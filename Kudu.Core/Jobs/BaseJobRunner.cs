using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public abstract class BaseJobRunner
    {
        private static readonly string[] AppConfigFilesLookupList = new string[] { "*.exe.config" };

        private readonly ExternalCommandFactory _externalCommandFactory;
        private readonly IAnalytics _analytics;
        private string _shutdownNotificationFilePath;

        protected BaseJobRunner(string jobName, string jobsTypePath, IEnvironment environment,
            IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
        {
            TraceFactory = traceFactory;
            Environment = environment;
            Settings = settings;
            JobName = jobName;
            _analytics = analytics;

            JobBinariesPath = Path.Combine(Environment.JobsBinariesPath, jobsTypePath, jobName);
            JobTempPath = Path.Combine(Environment.TempPath, Constants.JobsPath, jobsTypePath, jobName);
            JobDataPath = Path.Combine(Environment.DataPath, Constants.JobsPath, jobsTypePath, jobName);

            _externalCommandFactory = new ExternalCommandFactory(Environment, Settings, Environment.RepositoryPath);
        }

        protected IEnvironment Environment { get; private set; }

        protected IDeploymentSettingsManager Settings { get; private set; }

        protected ITraceFactory TraceFactory { get; private set; }

        public string JobName { get; private set; }

        protected string JobBinariesPath { get; private set; }

        protected string JobTempPath { get; private set; }

        protected string JobDataPath { get; private set; }

        protected string WorkingDirectory { get; private set; }

        protected abstract string JobEnvironmentKeyPrefix { get; }

        protected abstract TimeSpan IdleTimeout { get; }

        protected abstract void UpdateStatus(IJobLogger logger, string status);

        private static int CalculateHashForJob(string jobBinariesPath)
        {
            var updateDatesString = new StringBuilder();
            DirectoryInfoBase jobBinariesDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(jobBinariesPath);
            FileInfoBase[] files = jobBinariesDirectory.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (FileInfoBase file in files)
            {
                updateDatesString.Append(file.LastWriteTimeUtc.Ticks);
            }

            return updateDatesString.ToString().GetHashCode();
        }

        private void CacheJobBinaries(IJobLogger logger)
        {
            if (WorkingDirectory != null)
            {
                int currentHash = CalculateHashForJob(JobBinariesPath);
                int lastHash = CalculateHashForJob(WorkingDirectory);

                if (lastHash == currentHash)
                {
                    return;
                }
            }

            SafeKillAllRunningJobInstances(logger);

            if (FileSystemHelpers.DirectoryExists(JobTempPath))
            {
                FileSystemHelpers.DeleteDirectorySafe(JobTempPath, ignoreErrors: true);
            }

            if (FileSystemHelpers.DirectoryExists(JobTempPath))
            {
                logger.LogWarning("Failed to delete temporary directory");
            }

            try
            {
                var tempJobInstancePath = Path.Combine(JobTempPath, Path.GetRandomFileName());

                FileSystemHelpers.CopyDirectoryRecursive(JobBinariesPath, tempJobInstancePath);
                UpdateAppConfigs(tempJobInstancePath);

                WorkingDirectory = tempJobInstancePath;
            }
            catch (Exception ex)
            {
                //Status = "Worker is not running due to an error";
                //TraceError("Failed to copy bin directory: " + ex);
                logger.LogError("Failed to copy job files: " + ex);
                _analytics.UnexpectedException(ex);

                // job disabled
                WorkingDirectory = null;
            }
        }

        public string GetJobEnvironmentKey()
        {
            return JobEnvironmentKeyPrefix + JobName;
        }

        protected void InitializeJobInstance(JobBase job, IJobLogger logger)
        {
            if (!String.Equals(JobName, job.Name, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "The job runner can only run jobs with the same name it was configured, configured - {0}, trying to run - {1}".FormatInvariant(
                        JobName, job.Name));
            }

            if (!FileSystemHelpers.FileExists(job.ScriptFilePath))
            {
                throw new InvalidOperationException("Missing job script to run - {0}".FormatInvariant(job.ScriptFilePath));
            }

            CacheJobBinaries(logger);

            if (WorkingDirectory == null)
            {
                throw new InvalidOperationException("Missing working directory");
            }
        }

        protected void RunJobInstance(JobBase job, IJobLogger logger, string runId)
        {
            string scriptFileName = Path.GetFileName(job.ScriptFilePath);
            string scriptFileFullPath = Path.Combine(WorkingDirectory, job.RunCommand);
            string workingDirectoryForScript = Path.GetDirectoryName(scriptFileFullPath);

            logger.LogInformation("Run script '{0}' with script host - '{1}'".FormatCurrentCulture(scriptFileName, job.ScriptHost.GetType().Name));

            using (var jobStartedReporter = new JobStartedReporter(_analytics, job, Settings.GetWebSitePolicy(), JobDataPath))
            {
                try
                {
                    var exe = _externalCommandFactory.BuildCommandExecutable(job.ScriptHost.HostPath, workingDirectoryForScript, IdleTimeout, NullLogger.Instance);

                    _shutdownNotificationFilePath = RefreshShutdownNotificationFilePath(job.Name, job.JobType);

                    // Set environment variable to be able to identify all processes spawned for this job
                    exe.EnvironmentVariables[GetJobEnvironmentKey()] = "true";
                    exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsRootPath] = WorkingDirectory;
                    exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsName] = job.Name;
                    exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsType] = job.JobType;
                    exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsDataPath] = JobDataPath;
                    exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsRunId] = runId;
                    exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsShutdownNotificationFile] = _shutdownNotificationFilePath;

                    UpdateStatus(logger, "Running");

                    int exitCode =
                        exe.ExecuteReturnExitCode(
                            TraceFactory.GetTracer(),
                            logger.LogStandardOutput,
                            logger.LogStandardError,
                            job.ScriptHost.ArgumentsFormat,
                            scriptFileName);

                    if (exitCode != 0)
                    {
                        string errorMessage = "Job failed due to exit code " + exitCode;
                        logger.LogError(errorMessage);
                        jobStartedReporter.Error = errorMessage;
                    }
                    else
                    {
                        UpdateStatus(logger, "Success");
                    }
                }
                catch (Exception ex)
                {
                    if (ex is ThreadAbortException)
                    {
                        // We kill the process when refreshing the job
                        logger.LogInformation("Job aborted");
                        UpdateStatus(logger, "Aborted");
                        return;
                    }

                    logger.LogError(ex.ToString());

                    jobStartedReporter.Error = ex.Message;
                }
            }
        }

        public void SafeKillAllRunningJobInstances(IJobLogger logger)
        {
            try
            {
                Process[] processes = Process.GetProcesses();

                foreach (Process process in processes)
                {
                    StringDictionary processEnvironment;
                    bool success = ProcessEnvironment.TryGetEnvironmentVariables(process, out processEnvironment);
                    if (success && processEnvironment.ContainsKey(GetJobEnvironmentKey()))
                    {
                        try
                        {
                            process.Kill();
                        }
                        catch (Exception ex)
                        {
                            if (!process.HasExited)
                            {
                                logger.LogWarning("Failed to kill process - {0} for job - {1}\n{2}".FormatInvariant(process.ProcessName, JobName, ex));
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex.ToString());
            }
        }

        protected void NotifyShutdownJob()
        {
            try
            {
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(_shutdownNotificationFilePath));
                OperationManager.Attempt(() => FileSystemHelpers.WriteAllText(_shutdownNotificationFilePath, DateTime.UtcNow.ToString()));
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
                _analytics.UnexpectedException(ex);
            }
        }

        private string RefreshShutdownNotificationFilePath(string jobName, string jobsTypePath)
        {
            string shutdownFilesDirectory = Path.Combine(Environment.TempPath, "JobsShutdown", jobsTypePath, jobName);
            FileSystemHelpers.DeleteDirectoryContentsSafe(shutdownFilesDirectory, ignoreErrors: true);
            return Path.Combine(shutdownFilesDirectory, Path.GetRandomFileName());
        }

        private void UpdateAppConfigs(string tempJobInstancePath)
        {
            IEnumerable<string> configFilePaths = FileSystemHelpers.ListFiles(tempJobInstancePath, SearchOption.AllDirectories, AppConfigFilesLookupList);

            foreach (string configFilePath in configFilePaths)
            {
                UpdateAppConfig(configFilePath);
            }
        }

        private void UpdateAppConfig(string configFilePath)
        {
            try
            {
                var settings = SettingsProcessor.Instance;

                bool updateXml = false;

                // Read app.config
                string exeFilePath = configFilePath.Substring(0, configFilePath.Length - ".config".Length);
                Configuration config = ConfigurationManager.OpenExeConfiguration(exeFilePath);

                foreach (var appSetting in settings.AppSettings)
                {
                    config.AppSettings.Settings.Remove(appSetting.Key);
                    config.AppSettings.Settings.Add(appSetting.Key, appSetting.Value);
                    updateXml = true;
                }

                foreach (ConnectionStringSettings connectionString in settings.ConnectionStrings)
                {
                    ConnectionStringSettings currentConnectionString = config.ConnectionStrings.ConnectionStrings[connectionString.Name];
                    if (currentConnectionString != null)
                    {
                        // Update provider name if connection string already exists and provider name is null (custom type)
                        connectionString.ProviderName = connectionString.ProviderName ?? currentConnectionString.ProviderName;
                    }

                    config.ConnectionStrings.ConnectionStrings.Remove(connectionString.Name);
                    config.ConnectionStrings.ConnectionStrings.Add(connectionString);

                    updateXml = true;
                }

                if (updateXml)
                {
                    // Write updated app.config
                    config.Save();
                }
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
                _analytics.UnexpectedException(ex);
            }
        }

        /// <summary>
        /// This class will make sure the "JobStarted" analytics event is invoked after 5 seconds or when disposed.
        /// </summary>
        private sealed class JobStartedReporter : IDisposable
        {
            private static readonly int ReportTimeoutInMilliseconds = (int)TimeSpan.FromSeconds(5).TotalMilliseconds;

            private readonly IAnalytics _analytics;
            private readonly JobBase _job;
            private readonly string _siteMode;
            private readonly string _jobDataPath;

            private Timer _timer;
            private int _reported;

            public JobStartedReporter(IAnalytics analytics, JobBase job, string siteMode, string jobDataPath)
            {
                _analytics = analytics;
                _job = job;
                _siteMode = siteMode;
                _jobDataPath = jobDataPath;

                _timer = new Timer(Report, null, ReportTimeoutInMilliseconds, Timeout.Infinite);
            }

            public string Error { get; set; }

            private void Report(object state = null)
            {
                // Make sure this code is only called once.
                if (Interlocked.Exchange(ref _reported, 1) == 0)
                {
                    string scriptFileExtension = Path.GetExtension(_job.ScriptFilePath);
                    string jobType = _job.JobType;

                    // Recheck whether the job is marked as "using sdk" here since the SDK will create the marker file
                    // on the fly so it requires a "first run" first.
                    bool isUsingSdk = JobsManagerBase.IsUsingSdk(_jobDataPath);
                    if (isUsingSdk)
                    {
                        jobType += "/SDK";
                    }

                    _analytics.JobStarted(_job.Name.Fuzz(), scriptFileExtension, jobType, _siteMode, Error);
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            private void Dispose(bool disposing)
            {
                if (disposing)
                {
                    if (_timer != null)
                    {
                        _timer.Dispose();
                        _timer = null;
                    }

                    Report();
                }
            }
        }
    }
}

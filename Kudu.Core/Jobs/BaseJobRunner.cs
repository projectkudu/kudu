using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Xml.Linq;
using System.Xml.XPath;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
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
        private string _workingDirectory;
        private string _inPlaceWorkingDirectory;
        private Dictionary<string, FileInfoBase> _cachedSourceDirectoryFileMap;

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

        protected string WorkingDirectory
        {
            get { return _inPlaceWorkingDirectory ?? _workingDirectory; }
        }

        protected abstract string JobEnvironmentKeyPrefix { get; }

        protected abstract TimeSpan IdleTimeout { get; }

        protected abstract void UpdateStatus(IJobLogger logger, string status);

        protected JobSettings JobSettings { get; set; }

        internal static bool JobDirectoryHasChanged(
            Dictionary<string, FileInfoBase> sourceDirectoryFileMap, 
            Dictionary<string, FileInfoBase> workingDirectoryFileMap, 
            Dictionary<string, FileInfoBase> cachedSourceDirectoryFileMap)
        {
            // enumerate all source directory files, and compare against the files
            // in the working directory (i.e. the cached directory)
            FileInfoBase foundEntry = null;
            foreach (var entry in sourceDirectoryFileMap)
            {  
                if (workingDirectoryFileMap.TryGetValue(entry.Key, out foundEntry))
                {
                    if (entry.Value.LastWriteTimeUtc > foundEntry.LastWriteTimeUtc)
                    {
                        // source file has changed since we last cached it
                        return true;
                    }
                }
                else
                {
                    // a new file has been added that isn't in our cache directory
                    return true;
                }
            }

            // if we've previously run this check in the current process,
            // look for any file deletions by ensuring all our previously
            // cached entries are still present
            if (cachedSourceDirectoryFileMap != null)
            {
                foreach (var entry in cachedSourceDirectoryFileMap)
                {
                    if (!sourceDirectoryFileMap.TryGetValue(entry.Key, out foundEntry))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static Dictionary<string, FileInfoBase> GetJobDirectoryFileMap(string sourceDirectory)
        {
            Dictionary<string, FileInfoBase> fileMap = new Dictionary<string, FileInfoBase>();

            DirectoryInfoBase jobBinariesDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(sourceDirectory);
            FileInfoBase[] files = jobBinariesDirectory.GetFiles("*.*", SearchOption.AllDirectories);
            foreach (FileInfoBase file in files)
            {
                string key = file.FullName.Substring(sourceDirectory.Length + 1);
                fileMap.Add(key, file);
            }

            return fileMap;
        }

        private void CacheJobBinaries(JobBase job, IJobLogger logger)
        {
            bool isInPlaceDefault = job.ScriptHost.GetType() == typeof(NodeScriptHost);
            if (JobSettings.GetIsInPlace(isInPlaceDefault))
            {
                _inPlaceWorkingDirectory = JobBinariesPath;
                SafeKillAllRunningJobInstances(logger);
                UpdateAppConfigs(WorkingDirectory);
                return;
            }

            _inPlaceWorkingDirectory = null;

            Dictionary<string, FileInfoBase> sourceDirectoryFileMap = null;
            if (WorkingDirectory != null)
            {
                try
                {
                    sourceDirectoryFileMap = GetJobDirectoryFileMap(JobBinariesPath);
                    var workingDirectoryFileMap = GetJobDirectoryFileMap(WorkingDirectory);
                    if (!JobDirectoryHasChanged(sourceDirectoryFileMap, workingDirectoryFileMap, _cachedSourceDirectoryFileMap))
                    {
                        // no changes detected, so skip the cache/copy step below
                        return;
                    }
                }
                catch (Exception ex)
                {
                    // Log error and ignore it, since this diff optimization isn't critical.
                    // We'll just do a full copy in this case.
                    logger.LogWarning("Failed to diff WebJob directories for changes. Continuing to copy WebJob binaries (this will not affect the WebJob run)\n" + ex);
                    _analytics.UnexpectedException(ex);
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
                OperationManager.Attempt(() =>
                {
                    var tempJobInstancePath = Path.Combine(JobTempPath, Path.GetRandomFileName());

                    FileSystemHelpers.CopyDirectoryRecursive(JobBinariesPath, tempJobInstancePath);
                    UpdateAppConfigs(tempJobInstancePath);

                    _workingDirectory = tempJobInstancePath;

                    // cache the file map snapshot for next time (to aid in detecting
                    // file deletions)
                    _cachedSourceDirectoryFileMap = sourceDirectoryFileMap;
                });
            }
            catch (Exception ex)
            {
                //Status = "Worker is not running due to an error";
                //TraceError("Failed to copy bin directory: " + ex);
                logger.LogError("Failed to copy job files: " + ex);
                _analytics.UnexpectedException(ex);

                // job disabled
                _workingDirectory = null;
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

            CacheJobBinaries(job, logger);

            if (WorkingDirectory == null)
            {
                throw new InvalidOperationException("Missing working directory");
            }
        }

        protected void RunJobInstance(JobBase job, IJobLogger logger, string runId, string trigger, int port = -1)
        {
            string scriptFileName = Path.GetFileName(job.ScriptFilePath);
            string scriptFileFullPath = Path.Combine(WorkingDirectory, job.RunCommand);
            string workingDirectoryForScript = Path.GetDirectoryName(scriptFileFullPath);

            logger.LogInformation("Run script '{0}' with script host - '{1}'".FormatCurrentCulture(scriptFileName, job.ScriptHost.GetType().Name));

            using (var jobStartedReporter = new JobStartedReporter(_analytics, job, trigger, Settings.GetWebSiteSku(), JobDataPath))
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
                    exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsCommandArguments] = job.CommandArguments;
                    if (port != -1)
                    {
                        exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsPort] = port.ToString();
                    }

                    if (_shutdownNotificationFilePath != null)
                    {
                        exe.EnvironmentVariables[WellKnownEnvironmentVariables.WebJobsShutdownNotificationFile] = _shutdownNotificationFilePath;
                    }

                    UpdateStatus(logger, "Running");

                    int exitCode =
                        exe.ExecuteReturnExitCode(
                            TraceFactory.GetTracer(),
                            logger.LogStandardOutput,
                            logger.LogStandardError,
                            job.ScriptHost.ArgumentsFormat,
                            scriptFileName,
                            job.CommandArguments != null ? " " + job.CommandArguments : String.Empty);

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
                catch (ThreadAbortException)
                {
                    // We kill the process when refreshing the job
                    logger.LogInformation("WebJob process was aborted");
                    UpdateStatus(logger, "Stopped");
                }
                catch (Exception ex)
                {
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
                    Dictionary<string, string> processEnvironment;
                    bool success = process.TryGetEnvironmentVariables(out processEnvironment);
                    if (success && processEnvironment.ContainsKey(GetJobEnvironmentKey()))
                    {
                        try
                        {
                            process.Kill(true, TraceFactory.GetTracer());
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
                if (_shutdownNotificationFilePath != null)
                {
                    OperationManager.Attempt(() =>
                    {
                        FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(_shutdownNotificationFilePath));
                        FileSystemHelpers.WriteAllText(_shutdownNotificationFilePath, DateTime.UtcNow.ToString());
                    });
                }
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(ex);
            }
        }

        protected virtual string RefreshShutdownNotificationFilePath(string jobName, string jobsTypePath)
        {
            string shutdownFilesDirectory = Path.Combine(Environment.TempPath, "JobsShutdown", jobsTypePath, jobName);
            FileSystemHelpers.EnsureDirectory(shutdownFilesDirectory);
            FileSystemHelpers.DeleteDirectoryContentsSafe(shutdownFilesDirectory, ignoreErrors: true);
            return Path.Combine(shutdownFilesDirectory, Path.GetRandomFileName());
        }

        private void UpdateAppConfigs(string tempJobInstancePath)
        {
            IEnumerable<string> configFilePaths = FileSystemHelpers.ListFiles(tempJobInstancePath, SearchOption.AllDirectories, AppConfigFilesLookupList);

            foreach (string configFilePath in configFilePaths)
            {
                UpdateAppConfig(configFilePath);
                UpdateAppConfigAddTraceListeners(configFilePath);
            }
        }

        /// <summary>
        /// Updates the app.config using XML directly for injecting trace providers.
        /// </summary>
        private void UpdateAppConfigAddTraceListeners(string configFilePath)
        {
            try
            {
                var xmlConfig = XDocument.Load(configFilePath);

                // Make sure the trace listeners section available otherwise create it
                var configurationElement = GetOrCreateElement(xmlConfig, "configuration");
                var systemDiagnosticsElement = GetOrCreateElement(configurationElement, "system.diagnostics");
                var traceElement = GetOrCreateElement(systemDiagnosticsElement, "trace");
                var listenersElement = GetOrCreateElement(traceElement, "listeners");

                // Inject existing trace providers to the target app.config
                foreach (TraceListener listener in Trace.Listeners)
                {
                    // Ignore the default trace provider
                    if (String.Equals(listener.Name, "default", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Do not add a trace provider if it already exists (by name)
                    XElement listenerElement = listenersElement.Elements().FirstOrDefault(xElement =>
                    {
                        XAttribute nameAttribute = xElement.Attribute("name");
                        return nameAttribute != null && String.Equals(nameAttribute.Value, listener.Name, StringComparison.OrdinalIgnoreCase);
                    });

                    if (listenerElement == null)
                    {
                        var addElement = new XElement("add");
                        addElement.Add(new XAttribute("name", listener.Name));
                        addElement.Add(new XAttribute("type", listener.GetType().AssemblyQualifiedName));
                        listenersElement.AddFirst(addElement);
                    }
                }

                FileSystemHelpers.WriteAllText(configFilePath, xmlConfig.ToString());
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(ex);
            }
        }

        private static XElement GetOrCreateElement(XContainer root, string name)
        {
            var element = root.XPathSelectElement(name);

            if (element == null)
            {
                element = new XElement(name);
                root.Add(element);
            }

            return element;
        }

        private void UpdateAppConfig(string configFilePath)
        {
            try
            {
                var settings = SettingsProcessor.Instance;

                bool updateXml = false;

                // Read app.config
                string exeFilePath = configFilePath.Substring(0, configFilePath.Length - ".config".Length);

                // Only continue to update config file if the corresponding exe file exists
                if (!FileSystemHelpers.FileExists(exeFilePath))
                {
                    return;
                }

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
            private readonly string _trigger;
            private readonly string _siteMode;
            private readonly string _jobDataPath;

            private Timer _timer;
            private int _reported;

            public JobStartedReporter(IAnalytics analytics, JobBase job, string trigger, string siteMode, string jobDataPath)
            {
                _analytics = analytics;
                _job = job;
                _trigger = trigger;
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

                    _analytics.JobStarted(_job.Name.Fuzz(), scriptFileExtension, jobType, _siteMode, Error, _trigger);
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

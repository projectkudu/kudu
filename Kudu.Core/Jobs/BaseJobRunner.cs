using System;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Text;
using System.Threading;
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
        private readonly ExternalCommandFactory _externalCommandFactory;
        private readonly IAnalytics _analytics;

        protected BaseJobRunner(string jobName, string jobsTypePath, IEnvironment environment, IFileSystem fileSystem, IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
        {
            TraceFactory = traceFactory;
            Environment = environment;
            FileSystem = fileSystem;
            Settings = settings;
            JobName = jobName;
            _analytics = analytics;

            JobBinariesPath = Path.Combine(Environment.JobsBinariesPath, jobsTypePath, jobName);
            JobTempPath = Path.Combine(Environment.TempPath, Constants.JobsPath, jobsTypePath, jobName);
            JobDataPath = Path.Combine(Environment.DataPath, Constants.JobsPath, jobsTypePath, jobName);

            _externalCommandFactory = new ExternalCommandFactory(Environment, Settings, Environment.RepositoryPath);
        }

        protected IEnvironment Environment { get; private set; }

        protected IFileSystem FileSystem { get; private set; }

        protected IDeploymentSettingsManager Settings { get; private set; }

        protected ITraceFactory TraceFactory { get; private set; }

        protected string JobName { get; private set; }

        protected string JobBinariesPath { get; private set; }

        protected string JobTempPath { get; private set; }

        protected string JobDataPath { get; private set; }

        protected string WorkingDirectory { get; private set; }

        protected abstract string JobEnvironmentKeyPrefix { get; }

        protected abstract TimeSpan IdleTimeout { get; }

        protected abstract void UpdateStatus(IJobLogger logger, string status);

        private int CalculateHashForJob(string jobBinariesPath)
        {
            var updateDatesString = new StringBuilder();
            DirectoryInfoBase jobBinariesDirectory = FileSystem.DirectoryInfo.FromDirectoryName(jobBinariesPath);
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

            if (FileSystem.Directory.Exists(JobTempPath))
            {
                FileSystemHelpers.DeleteDirectorySafe(JobTempPath, true);
            }

            if (FileSystem.Directory.Exists(JobTempPath))
            {
                logger.LogWarning("Failed to delete temporary directory");
            }

            try
            {
                var tempJobInstancePath = Path.Combine(JobTempPath, Path.GetRandomFileName());

                FileSystemHelpers.CopyDirectoryRecursive(FileSystem, JobBinariesPath, tempJobInstancePath);

                WorkingDirectory = tempJobInstancePath;
            }
            catch (Exception ex)
            {
                //Status = "Worker is not running due to an error";
                //TraceError("Failed to copy bin directory: " + ex);
                logger.LogError("Failed to copy job files: " + ex);

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

            if (!FileSystem.File.Exists(job.ScriptFilePath))
            {
                //Status = "Missing run_worker.cmd file";
                //Trace.TraceError(Status);
                throw new InvalidOperationException("Missing job script to run - {0}".FormatInvariant(job.ScriptFilePath));
            }

            CacheJobBinaries(logger);

            if (WorkingDirectory == null)
            {
                throw new InvalidOperationException("Missing working directory");
            }
        }

        protected void RunJobInstance(JobBase job, IJobLogger logger)
        {
            string scriptFileName = Path.GetFileName(job.ScriptFilePath);
            string scriptFileExtension = Path.GetExtension(job.ScriptFilePath);

            logger.LogInformation("Run script '{0}' with script host - '{1}'".FormatCurrentCulture(scriptFileName, job.ScriptHost.GetType().Name));
            _analytics.JobStarted(job.Name.Fuzz(), scriptFileExtension, job.JobType);

            try
            {
                var exe = _externalCommandFactory.BuildCommandExecutable(job.ScriptHost.HostPath, WorkingDirectory, IdleTimeout, NullLogger.Instance);

                // Set environment variable to be able to identify all processes spawned for this job
                exe.EnvironmentVariables[GetJobEnvironmentKey()] = "true";
                exe.EnvironmentVariables[WellKnownEnvironmentVariables.JobRootPath] = WorkingDirectory;
                exe.EnvironmentVariables[WellKnownEnvironmentVariables.JobName] = job.Name;
                exe.EnvironmentVariables[WellKnownEnvironmentVariables.JobType] = job.JobType;
                exe.EnvironmentVariables[WellKnownEnvironmentVariables.JobDataPath] = JobDataPath;
                exe.EnvironmentVariables[WellKnownEnvironmentVariables.JobExtraUrlPath] = JobsManagerBase.GetJobExtraInfoUrlFilePath(JobDataPath);

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
                    logger.LogError("Job failed due to exit code " + exitCode);
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
                    return;
                }

                logger.LogError(ex.ToString());
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
    }
}
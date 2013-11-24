using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class TriggeredJobRunner : BaseJobRunner
    {
        private readonly LockFile _lockFile;

        public TriggeredJobRunner(string jobName, IEnvironment environment, IFileSystem fileSystem, IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
            : base(jobName, Constants.TriggeredPath, environment, fileSystem, settings, traceFactory, analytics)
        {
            _lockFile = new LockFile(Path.Combine(JobDataPath, "triggeredJob.lock"), TraceFactory, FileSystem);
        }

        protected override string JobEnvironmentKeyPrefix
        {
            get { return "WEBSITE_TRIGGERED_JOB_RUNNING_"; }
        }

        protected override TimeSpan IdleTimeout
        {
            get { return Settings.GetJobsIdleTimeout(); }
        }

        public void StartJobRun(TriggeredJob triggeredJob)
        {
            if (!_lockFile.Lock())
            {
                throw new ConflictException();
            }

            TriggeredJobRunLogger logger = TriggeredJobRunLogger.LogNewRun(triggeredJob, Environment, FileSystem, TraceFactory, Settings);
            Debug.Assert(logger != null);

            try
            {
                InitializeJobInstance(triggeredJob, logger);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        RunJobInstance(triggeredJob, logger);
                    }
                    finally
                    {
                        logger.ReportEndRun();
                        _lockFile.Release();
                    }
                });
            }
            catch (Exception ex)
            {
                logger.LogError("Failed to start job due to: " + ex);
                _lockFile.Release();
                throw;
            }
        }

        protected override void UpdateStatus(IJobLogger logger, string status)
        {
            ((TriggeredJobRunLogger)logger).ReportStatus(status);
        }
    }
}


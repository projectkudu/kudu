using System;
using System.Diagnostics;
using System.IO;
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

        public TriggeredJobRunner(string jobName, IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
            : base(jobName, Constants.TriggeredPath, environment, settings, traceFactory, analytics)
        {
            _lockFile = BuildTriggeredJobRunnerLockFile(JobDataPath, TraceFactory);
        }

        public static LockFile BuildTriggeredJobRunnerLockFile(string jobDataPath, ITraceFactory traceFactory)
        {
            return new LockFile(Path.Combine(jobDataPath, "triggeredJob.lock"), traceFactory);
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

            TriggeredJobRunLogger logger = TriggeredJobRunLogger.LogNewRun(triggeredJob, Environment, TraceFactory, Settings);
            Debug.Assert(logger != null);

            try
            {
                InitializeJobInstance(triggeredJob, logger);

                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        RunJobInstance(triggeredJob, logger, logger.Id);
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

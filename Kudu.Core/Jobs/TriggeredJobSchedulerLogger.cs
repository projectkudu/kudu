using System;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class TriggeredJobSchedulerLogger : FilePerJobLogger
    {
        public const string LogFileName = "job_scheduler.log";

        public TriggeredJobSchedulerLogger(string jobName, IEnvironment environment, ITraceFactory traceFactory)
            : base(jobName, Constants.TriggeredPath, "status", LogFileName, environment, traceFactory)
        {
        }

        public override void LogStandardOutput(string message)
        {
            throw new NotSupportedException();
        }

        public override void LogStandardError(string message)
        {
            throw new NotSupportedException();
        }

        protected override void OnRolledLogFile()
        {
        }

        protected override void ClearJobsListCache()
        {
            TriggeredJobsManager.ClearJobListCache();
        }
    }
}

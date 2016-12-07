using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class TriggeredJobRunLogger : JobLogger
    {
        public const string TriggeredStatusFile = "status";
        public const string OutputLogFileName = "output_log.txt";
        public const string ErrorLogFileName = "error_log.txt";

        private readonly string _id;
        private readonly string _historyPath;
        private readonly string _outputFilePath;

        private TriggeredJobRunLogger(string jobName, string id, IEnvironment environment, ITraceFactory traceFactory)
            : base(TriggeredStatusFile, environment, traceFactory)
        {
            _id = id;

            _historyPath = Path.Combine(Environment.JobsDataPath, Constants.TriggeredPath, jobName, _id);
            FileSystemHelpers.EnsureDirectory(_historyPath);

            _outputFilePath = Path.Combine(_historyPath, OutputLogFileName);
        }

        [SuppressMessage("Microsoft.Design", "CA1011:ConsiderPassingBaseTypesAsParameters", Justification = "We do not want to accept jobs which are not TriggeredJob")]
        public static TriggeredJobRunLogger LogNewRun(TriggeredJob triggeredJob, string trigger, IEnvironment environment, ITraceFactory traceFactory, IDeploymentSettingsManager settings)
        {
            OldRunsCleanup(triggeredJob.Name, environment, traceFactory, settings);

            string id = DateTime.UtcNow.ToString("yyyyMMddHHmmssffff");
            var logger = new TriggeredJobRunLogger(triggeredJob.Name, id, environment, traceFactory);
            var triggeredJobStatus = new TriggeredJobStatus
            {
                Trigger = trigger,
                Status = JobStatus.Initializing,
                StartTime = DateTime.UtcNow
            };
            logger.ReportStatus(triggeredJobStatus);
            return logger;
        }

        private static void OldRunsCleanup(string jobName, IEnvironment environment, ITraceFactory traceFactory, IDeploymentSettingsManager settings)
        {
            // if max is 5 and we have 5 we still want to remove one to make room for the next
            // that's why we decrement max value by 1
            int maxRuns = settings.GetWebJobsHistorySize() - 1;

            string historyPath = Path.Combine(environment.JobsDataPath, Constants.TriggeredPath, jobName);
            DirectoryInfoBase historyDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(historyPath);
            if (!historyDirectory.Exists)
            {
                return;
            }

            DirectoryInfoBase[] historyRunsDirectories = historyDirectory.GetDirectories();
            if (historyRunsDirectories.Length <= maxRuns)
            {
                return;
            }

            var directoriesToRemove = historyRunsDirectories.OrderByDescending(d => d.Name).Skip(maxRuns);
            foreach (DirectoryInfoBase directory in directoriesToRemove)
            {
                try
                {
                    directory.Delete(true);
                }
                catch (Exception ex)
                {
                    traceFactory.GetTracer().TraceError(ex);
                }
            }
        }

        public void ReportEndRun()
        {
            var triggeredJobStatus = ReadJobStatusFromFile<TriggeredJobStatus>(Analytics, GetStatusFilePath()) ?? new TriggeredJobStatus();
            triggeredJobStatus.EndTime = DateTime.UtcNow;
            ReportStatus(triggeredJobStatus, logStatus: false);
        }

        public void ReportStatus(string status)
        {
            var triggeredJobStatus = ReadJobStatusFromFile<TriggeredJobStatus>(Analytics, GetStatusFilePath()) ?? new TriggeredJobStatus();
            triggeredJobStatus.Status = status;
            ReportStatus(triggeredJobStatus);
        }

        protected override void ClearJobsListCache()
        {
            TriggeredJobsManager.ClearJobListCache();
        }

        public string Id
        {
            get
            {
                return _id;
            }
        }

        protected override string HistoryPath
        {
            get { return _historyPath; }
        }

        public override void LogError(string error)
        {
            var triggeredJobStatus = ReadJobStatusFromFile<TriggeredJobStatus>(Analytics, GetStatusFilePath()) ?? new TriggeredJobStatus();
            triggeredJobStatus.Status = JobStatus.Failed;
            ReportStatus(triggeredJobStatus);
            Log(Level.Err, error, isSystem: true);
        }

        public override void LogWarning(string warning)
        {
            Log(Level.Warn, warning, isSystem: true);
        }

        public override void LogInformation(string message)
        {
            Log(Level.Info, message, isSystem: true);
        }

        public override void LogStandardOutput(string message)
        {
            Log(Level.Info, message, isSystem: false);
        }

        public override void LogStandardError(string message)
        {
            Log(Level.Err, message, isSystem: false);
        }

        private void Log(Level level, string message, bool isSystem)
        {
            message = GetFormattedMessage(level, message, isSystem);

            SafeLogToFile(_outputFilePath, message);
        }
    }
}
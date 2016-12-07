using System;
using System.IO;
using System.IO.Abstractions;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;

namespace Kudu.Core.Jobs
{
    public abstract class JobLogger : IJobLogger
    {
        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None
        };

        protected IEnvironment Environment { get; private set; }

        protected ITraceFactory TraceFactory { get; private set; }

        protected IAnalytics Analytics { get; private set; }

        protected string InstanceId { get; private set; }

        private string _statusFilePath;

        private readonly string _statusFileName;

        protected JobLogger(string statusFileName, IEnvironment environment, ITraceFactory traceFactory)
        {
            _statusFileName = statusFileName;
            TraceFactory = traceFactory;
            Environment = environment;
            Analytics = new Analytics(null, new ServerConfiguration(), traceFactory);

            InstanceId = InstanceIdUtility.GetShortInstanceId();
        }

        public abstract void LogError(string error);

        public abstract void LogWarning(string warning);

        public abstract void LogInformation(string message);

        public abstract void LogStandardOutput(string message);

        public abstract void LogStandardError(string message);

        protected abstract string HistoryPath { get; }

        protected abstract void ClearJobsListCache();

        protected string GetStatusFilePath()
        {
            if (_statusFilePath == null)
            {
                _statusFilePath = Path.Combine(HistoryPath, _statusFileName);
            }

            return _statusFilePath;
        }

        public TJobStatus GetStatus<TJobStatus>() where TJobStatus : class, IJobStatus
        {
            return ReadJobStatusFromFile<TJobStatus>(Analytics, GetStatusFilePath());
        }

        public void ReportStatus<TJobStatus>(TJobStatus status) where TJobStatus : class, IJobStatus
        {
            ReportStatus(status, logStatus: true);
        }

        protected virtual void ReportStatus<TJobStatus>(TJobStatus status, bool logStatus) where TJobStatus : class, IJobStatus
        {
            try
            {
                string content = JsonConvert.SerializeObject(status, JsonSerializerSettings);
                SafeLogToFile(GetStatusFilePath(), content, isAppend: false);
                if (logStatus)
                {
                    LogInformation("Status changed to " + status.Status);
                }

                // joblistcache has info about job status, so when changing the status
                // the cache should be invalidated.
                ClearJobsListCache();
            }
            catch (Exception ex)
            {
                Analytics.UnexpectedException(ex);
            }
        }

        public static TJobStatus ReadJobStatusFromFile<TJobStatus>(IAnalytics analytics, string statusFilePath) where TJobStatus : class, IJobStatus
        {
            try
            {
                if (!FileSystemHelpers.FileExists(statusFilePath))
                {
                    return null;
                }

                // since we don't have proper lock on file, we are more forgiving in retry (10 times 250 ms interval).
                return OperationManager.Attempt(() =>
                {
                    string content = FileSystemHelpers.ReadAllTextFromFile(statusFilePath).Trim();
                    return JsonConvert.DeserializeObject<TJobStatus>(content, JsonSerializerSettings);
                }, retries: 10);
            }
            catch (Exception ex)
            {
                analytics.UnexpectedException(ex);
                return null;
            }
        }

        protected void SafeLogToFile(string path, string content, bool isAppend = true)
        {
            try
            {
                // since we don't have proper lock on file, we are more forgiving in retry (10 times 250 ms interval).
                if (isAppend)
                {
                    OperationManager.Attempt(() => FileSystemHelpers.AppendAllTextToFile(path, content), retries: 10);
                }
                else
                {
                    OperationManager.Attempt(() => FileSystemHelpers.WriteAllTextToFile(path, content), retries: 10);
                }
            }
            catch (Exception ex)
            {
                Analytics.UnexpectedException(ex);
            }
        }

        protected enum Level
        {
            Info,
            Warn,
            Err
        }

        protected string GetFormattedMessage(Level level, string message, bool isSystem)
        {
            return isSystem
                ? "[{0} > {1}: SYS {2,-4}] {3}\r\n".FormatInvariant(DateTime.UtcNow, InstanceId, level.ToString().ToUpperInvariant(), message)
                : "[{0} > {1}: {2,-4}] {3}\r\n".FormatInvariant(DateTime.UtcNow, InstanceId, level.ToString().ToUpperInvariant(), message);
        }
    }
}
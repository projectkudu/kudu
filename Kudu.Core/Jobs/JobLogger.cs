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
        public const string StatusFile = "status";

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings()
        {
            DefaultValueHandling = DefaultValueHandling.Ignore,
            Formatting = Formatting.None,
            MissingMemberHandling = MissingMemberHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore,
            PreserveReferencesHandling = PreserveReferencesHandling.None
        };

        protected IEnvironment Environment { get; private set; }

        protected IFileSystem FileSystem { get; private set; }

        protected ITraceFactory TraceFactory { get; private set; }

        protected string InstanceId { get; private set; }

        private string _statusFilePath;

        protected JobLogger(IEnvironment environment, IFileSystem fileSystem, ITraceFactory traceFactory)
        {
            TraceFactory = traceFactory;
            FileSystem = fileSystem;
            Environment = environment;

            InstanceId = InstanceIdUtility.GetShortInstanceId();
        }

        public abstract void LogError(string error);

        public abstract void LogWarning(string warning);

        public abstract void LogInformation(string message);

        public abstract void LogStandardOutput(string message);

        public abstract void LogStandardError(string message);

        protected abstract string HistoryPath { get; }

        protected string GetStatusFilePath()
        {
            if (_statusFilePath == null)
            {
                _statusFilePath = Path.Combine(HistoryPath, StatusFile);
            }

            return _statusFilePath;
        }

        public void ReportStatus<TJobStatus>(TJobStatus status) where TJobStatus : class, IJobStatus
        {
            ReportStatus(status, logStatus: true);
        }

        protected void ReportStatus<TJobStatus>(TJobStatus status, bool logStatus) where TJobStatus : class, IJobStatus
        {
            try
            {
                string content = JsonConvert.SerializeObject(status, JsonSerializerSettings);
                SafeLogToFile(GetStatusFilePath(), content, isAppend: false);
                if (logStatus)
                {
                    LogInformation("Status changed to " + status.Status);
                }
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
            }
        }

        public static TJobStatus ReadJobStatusFromFile<TJobStatus>(ITraceFactory traceFactory, IFileSystem fileSystem, string statusFilePath) where TJobStatus : class, IJobStatus
        {
            try
            {
                if (!fileSystem.File.Exists(statusFilePath))
                {
                    return null;
                }

                return OperationManager.Attempt(() =>
                {
                    string content = fileSystem.File.ReadAllText(statusFilePath).Trim();
                    return JsonConvert.DeserializeObject<TJobStatus>(content, JsonSerializerSettings);
                });
            }
            catch (Exception ex)
            {
                traceFactory.GetTracer().TraceError(ex);
                return null;
            }
        }

        protected void SafeLogToFile(string path, string content, bool isAppend = true)
        {
            try
            {
                if (isAppend)
                {
                    OperationManager.Attempt(() => File.AppendAllText(path, content));
                }
                else
                {
                    OperationManager.Attempt(() => File.WriteAllText(path, content));
                }
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
            }
        }

        protected enum Level
        {
            Info,
            Warn,
            Err
        }

        protected string GetSystemFormattedMessage(Level level, string message)
        {
            return "[{0} > {1}: SYS {2,-4}] {3}\r\n".FormatInvariant(DateTime.UtcNow, InstanceId, level.ToString().ToUpperInvariant(), message);
        }
    }
}
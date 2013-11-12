using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobLogger : JobLogger
    {
        private readonly string _historyPath;
        private readonly string _logFilePath;

        public ContinuousJobLogger(string jobName, IEnvironment environment, IFileSystem fileSystem, ITraceFactory traceFactory)
            : base(environment, fileSystem, traceFactory)
        {
            _historyPath = Path.Combine(Environment.JobsDataPath, Constants.ContinuousPath, jobName);
            FileSystemHelpers.EnsureDirectory(_historyPath);
            _logFilePath = Path.Combine(_historyPath, "job.log");
        }

        protected override string HistoryPath
        {
            get
            {
                FileSystemHelpers.EnsureDirectory(_historyPath);
                return _historyPath;
            }
        }

        public override void LogError(string error)
        {
            Log(Level.Err, error);
        }

        public override void LogWarning(string warning)
        {
            Log(Level.Warn, warning);
        }

        public override void LogInformation(string message)
        {
            Log(Level.Info, message);
        }

        public override void LogStandardOutput(string message)
        {
            Trace.TraceInformation(message);
        }

        public override void LogStandardError(string message)
        {
            Trace.TraceError(message);
        }

        private void Log(Level level, string message)
        {
            SafeLogToFile(_logFilePath, GetSystemFormattedMessage(level, message));
        }
    }
}
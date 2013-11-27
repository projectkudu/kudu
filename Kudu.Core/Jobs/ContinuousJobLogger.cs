using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobLogger : JobLogger
    {
        public const string JobLogFileName = "job.log";
        public const string JobPrevLogFileName = "job_prev.log";
        public const int MaxContinuousLogFileSize = 1 * 1024 * 1024;

        private readonly string _historyPath;
        private readonly string _logFilePath;

        public ContinuousJobLogger(string jobName, IEnvironment environment, IFileSystem fileSystem, ITraceFactory traceFactory)
            : base(environment, fileSystem, traceFactory)
        {
            _historyPath = Path.Combine(Environment.JobsDataPath, Constants.ContinuousPath, jobName);
            FileSystemHelpers.EnsureDirectory(_historyPath);
            _logFilePath = GetLogFilePath(JobLogFileName);
        }

        private string GetLogFilePath(string logFileName)
        {
            return Path.Combine(_historyPath, logFileName);
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
            CleanupLogFileIfNeeded();
            SafeLogToFile(_logFilePath, GetSystemFormattedMessage(level, message));
        }

        private void CleanupLogFileIfNeeded()
        {
            try
            {
                FileInfoBase logFile = FileSystem.FileInfo.FromFileName(_logFilePath);

                if (logFile.Length > MaxContinuousLogFileSize)
                {
                    // lock file and only allow deleting it
                    // this is for allowing only the first (instance) trying to roll the log file
                    using (File.Open(_logFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
                    {
                        // roll log file, currently allow only 2 log files to exist at the same time
                        string prevLogFilePath = GetLogFilePath(JobPrevLogFileName);
                        FileSystem.File.Delete(prevLogFilePath);
                        logFile.MoveTo(prevLogFilePath);
                    }
                }
            }
            catch
            {
                // best effort for this method
            }
        }
    }
}
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public abstract class FilePerJobLogger : JobLogger
    {
        public const string JobPrevLogFileName = "job_prev_log.txt";
        public const int MaxLogFileSize = 1 * 1024 * 1024;
        public const int MaxConsoleLogLines = 200;

        private readonly string _historyPath;
        private readonly string _logFilePath;

        protected FilePerJobLogger(string jobName, string jobType, string statusFileName, string jobLogFileName, IEnvironment environment, ITraceFactory traceFactory)
            : base(statusFileName, environment, traceFactory)
        {
            _historyPath = Path.Combine(environment.JobsDataPath, jobType, jobName);
            FileSystemHelpers.EnsureDirectory(_historyPath);

            _logFilePath = GetLogFilePath(jobLogFileName);
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

        protected abstract void OnRolledLogFile();

        protected void Log(Level level, string message, bool isSystem)
        {
            CleanupLogFileIfNeeded();
            SafeLogToFile(_logFilePath, GetFormattedMessage(level, message, isSystem));
        }

        private void CleanupLogFileIfNeeded()
        {
            try
            {
                FileInfoBase logFile = FileSystemHelpers.FileInfoFromFileName(_logFilePath);

                if (logFile.Length > MaxLogFileSize)
                {
                    // lock file and only allow deleting it
                    // this is for allowing only the first (instance) trying to roll the log file
                    using (File.Open(_logFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
                    {
                        // roll log file, currently allow only 2 log files to exist at the same time
                        string prevLogFilePath = GetLogFilePath(JobPrevLogFileName);
                        FileSystemHelpers.DeleteFileSafe(prevLogFilePath);
                        logFile.MoveTo(prevLogFilePath);

                        OnRolledLogFile();
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

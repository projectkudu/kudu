using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobLogger : JobLogger, IDisposable
    {
        public const string JobLogFileName = "job_log.txt";
        public const string JobPrevLogFileName = "job_prev_log.txt";
        public const int MaxContinuousLogFileSize = 1 * 1024 * 1024;
        public const int MaxConsoleLogLines = 200;

        private readonly string _historyPath;
        private readonly string _logFilePath;

        private FileStream _lockedStatusFile;

        private int _consoleLogLinesCount;

        public ContinuousJobLogger(string jobName, IEnvironment environment, ITraceFactory traceFactory)
            : base(GetStatusFileName(), environment, traceFactory)
        {
            _historyPath = Path.Combine(Environment.JobsDataPath, Constants.ContinuousPath, jobName);
            FileSystemHelpers.EnsureDirectory(_historyPath);

            // Lock status file (allowing read and write but not delete) as a way to notify that this status file is valid (shows status of a current working instance)
            _logFilePath = GetLogFilePath(JobLogFileName);
            ResetLockedStatusFile();
        }

        private void ResetLockedStatusFile()
        {
            try
            {
                if (_lockedStatusFile != null)
                {
                    _lockedStatusFile.Dispose();
                }
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
            }

            _lockedStatusFile = File.Open(GetStatusFilePath(), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
        }

        protected override void ReportStatus<TJobStatus>(TJobStatus status, bool logStatus)
        {
            try
            {
                if (!FileSystemHelpers.FileExists(GetStatusFilePath()))
                {
                    ResetLockedStatusFile();
                }
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
            }

            base.ReportStatus(status, logStatus);
        }

        internal static string GetStatusFileName()
        {
            return ContinuousJobStatus.FileNamePrefix + InstanceIdUtility.GetShortInstanceId();
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

        public override void LogStandardOutput(string message)
        {
            Trace.TraceInformation(message);
            LogConsole(message, Level.Info);
        }

        public override void LogStandardError(string message)
        {
            Trace.TraceError(message);
            LogConsole(message, Level.Err);
        }

        public void StartingNewRun()
        {
            // Reset log lines count
            _consoleLogLinesCount = 0;
        }

        private void LogConsole(string message, Level level)
        {
            if (_consoleLogLinesCount < MaxConsoleLogLines)
            {
                _consoleLogLinesCount++;
                Log(level, message, isSystem: false);
            }
            else if (_consoleLogLinesCount == MaxConsoleLogLines)
            {
                _consoleLogLinesCount++;
                Log(Level.Warn, Resources.Log_MaxJobLogLinesReached, isSystem: false);
            }
        }

        private void Log(Level level, string message, bool isSystem)
        {
            CleanupLogFileIfNeeded();
            SafeLogToFile(_logFilePath, GetFormattedMessage(level, message, isSystem));
        }

        private void CleanupLogFileIfNeeded()
        {
            try
            {
                FileInfoBase logFile = FileSystemHelpers.FileInfoFromFileName(_logFilePath);

                if (logFile.Length > MaxContinuousLogFileSize)
                {
                    // lock file and only allow deleting it
                    // this is for allowing only the first (instance) trying to roll the log file
                    using (File.Open(_logFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Delete))
                    {
                        // roll log file, currently allow only 2 log files to exist at the same time
                        string prevLogFilePath = GetLogFilePath(JobPrevLogFileName);
                        FileSystemHelpers.DeleteFileSafe(prevLogFilePath);
                        logFile.MoveTo(prevLogFilePath);
                    }
                }
            }
            catch
            {
                // best effort for this method
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_lockedStatusFile != null)
                {
                    _lockedStatusFile.Dispose();
                    _lockedStatusFile = null;
                }
            }
        }
    }
}
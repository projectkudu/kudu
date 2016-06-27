using System;
using System.Diagnostics;
using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobLogger : FilePerJobLogger, IDisposable
    {
        public const int MaxContinuousLogFileSize = 1 * 1024 * 1024;
        public const string JobLogFileName = "job_log.txt";

        private FileStream _lockedStatusFile;

        private int _consoleLogLinesCount;

        public ContinuousJobLogger(string jobName, IEnvironment environment, ITraceFactory traceFactory)
            : base(jobName, Constants.ContinuousPath, GetStatusFileName(), JobLogFileName, environment, traceFactory)
        {
            // Lock status file (allowing read and write but not delete) as a way to notify that this status file is valid (shows status of a current working instance)
            ResetLockedStatusFile();
        }

        public event Action RolledLogFile;

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

            try
            {
                _lockedStatusFile = File.Open(GetStatusFilePath(), FileMode.OpenOrCreate, FileAccess.Read, FileShare.ReadWrite);
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
                throw;
            }
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

        protected override void ClearJobsListCache()
        {
            ContinuousJobsManager.ClearJobListCache();
        }

        internal static string GetStatusFileName()
        {
            return ContinuousJobStatus.FileNamePrefix + InstanceIdUtility.GetShortInstanceId();
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

        protected override void OnRolledLogFile()
        {
            Action rollEventHandler = RolledLogFile;
            if (rollEventHandler != null)
            {
                rollEventHandler();
            }
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

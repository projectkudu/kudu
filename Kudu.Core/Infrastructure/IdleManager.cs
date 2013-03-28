using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Infrastructure
{
    internal class IdleManager
    {
        private const int InitialWaitPeriod = 3 * 60 * 1000;
        private const int WaitInterval = 10 * 1000;
        private static readonly TimeSpan WaitIntervalTimeSpan = TimeSpan.FromMilliseconds(WaitInterval);
        private readonly string _processName;
        private readonly TimeSpan _idleTimeout;
        private readonly ITracer _tracer;
        private DateTime _lastActivity;

        public IdleManager(string path, TimeSpan idleTimeout, ITracer tracer)
        {
            _processName = Path.GetFileName(path);
            _idleTimeout = idleTimeout;
            _tracer = tracer;
            _lastActivity = DateTime.UtcNow;
        }

        public void UpdateActivity()
        {
            _lastActivity = DateTime.UtcNow;
        }

        public void WaitForExit(Process process)
        {
            // For the duration of the idle timeout, do nothing. Simply wait for the process to execute.
            if (!process.WaitForExit((int)_idleTimeout.TotalMilliseconds))
            {
                long previousCpuUsage = process.GetTotalProcessorTime();

                TimeSpan totalWaitDuration = _idleTimeout;
                while (!process.WaitForExit(WaitInterval))
                {
                    totalWaitDuration += WaitIntervalTimeSpan;
                    if (totalWaitDuration >= Constants.MaxAllowedExecutionTime)
                    {
                        // The duration a process is executing is capped. If it exceeds this period, we'll kill it regardless of it actually performing any activity.
                        ThrowIdleTimeoutException(process, totalWaitDuration);
                    }

                    // Did we see any IO activity during the last wait interval?
                    if (DateTime.UtcNow > _lastActivity.Add(WaitIntervalTimeSpan))
                    {
                        // There wasn't any IO activity. Check if we had any CPU activity
                        long currentCpuUsage = process.GetTotalProcessorTime();
                        if (currentCpuUsage != previousCpuUsage)
                        {
                            // The process performed some compute bound operation. We'll wait for it some more
                            previousCpuUsage = currentCpuUsage;
                            continue;
                        }

                        // It's likely that the process is idling waiting for user input. Kill it
                        process.Kill(true, _tracer);
                        ThrowIdleTimeoutException(process, totalWaitDuration);
                        break;
                    }
                }
            }

            // Once we are here, the process has terminated.  This extra WaitForExit with -1 timeout
            // will ensure in-memory Output buffer is flushed, from reflection, this.output.WaitUtilEOF().  
            // If we don't do this, the leftover output will write concurrently to the logger 
            // with the main thread corrupting the log xml.  
            process.WaitForExit(-1);
        }

        private void ThrowIdleTimeoutException(Process process, TimeSpan totalWaitDuration)
        {
            string arguments = (process.StartInfo.Arguments ?? String.Empty).Trim();
            if (arguments.Length > 15)
            {
                arguments = arguments.Substring(0, 15) + " ...";
            }
            string message = String.Format(Resources.Error_ProcessAborted, _processName + " " + arguments, totalWaitDuration.TotalSeconds);
            throw new CommandLineException(process.StartInfo.FileName, process.StartInfo.Arguments, message)
            {
                ExitCode = -1,
                Output = message,
                Error = message
            };
        }
    }
}

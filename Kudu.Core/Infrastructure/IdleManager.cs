using System;
using System.Diagnostics;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Infrastructure
{
    internal class IdleManager
    {
        private static readonly TimeSpan WaitIntervalTimeSpan = TimeSpan.FromSeconds(10);
        private readonly TimeSpan _idleTimeout;
        private readonly ITracer _tracer;
        private DateTime _lastActivity;

        public IdleManager(TimeSpan idleTimeout, ITracer tracer)
            : this(idleTimeout, tracer, DateTime.UtcNow)
        {
        }

        internal IdleManager(TimeSpan idleTimeout, ITracer tracer, DateTime dateTime)
        {
            _idleTimeout = idleTimeout;
            _tracer = tracer;
            _lastActivity = dateTime;
        }

        public void UpdateActivity()
        {
            _lastActivity = DateTime.UtcNow;
        }

        public void WaitForExit(Process process)
        {
            var processWrapper = new ProcessWrapper(process);
            WaitForExit(processWrapper);
        }

        internal void WaitForExit(IProcess process)
        {
            // For the duration of the idle timeout, do nothing. Simply wait for the process to execute.
            if (!process.WaitForExit(_idleTimeout))
            {
                long previousCpuUsage = process.GetTotalProcessorTime();

                TimeSpan totalWaitDuration = _idleTimeout;
                while (!process.WaitForExit(WaitIntervalTimeSpan))
                {
                    totalWaitDuration += WaitIntervalTimeSpan;
                    if (totalWaitDuration >= Constants.MaxAllowedExecutionTime)
                    {
                        process.Kill(_tracer);

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
                        process.Kill(_tracer);
                        ThrowIdleTimeoutException(process, totalWaitDuration);
                        break;
                    }
                }
            }

            process.WaitUntilEOF();
        }

        private static void ThrowIdleTimeoutException(IProcess process, TimeSpan totalWaitDuration)
        {
            string arguments = (process.Arguments ?? String.Empty).Trim();
            if (arguments.Length > 15)
            {
                arguments = arguments.Substring(0, 15) + " ...";
            }
            string message = String.Format(Resources.Error_ProcessAborted, process.Name + " " + arguments, totalWaitDuration.TotalSeconds);
            throw new CommandLineException(process.Name, process.Arguments, message)
            {
                ExitCode = -1,
                Output = message,
                Error = message
            };
        }
    }
}

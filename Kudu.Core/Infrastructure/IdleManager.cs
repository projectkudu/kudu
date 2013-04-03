using System;
using System.Diagnostics;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Infrastructure
{
    internal class IdleManager
    {
        private static readonly TimeSpan _initialCpuUsage = TimeSpan.FromSeconds(-1);

        private readonly TimeSpan _idleTimeout;
        private readonly ITracer _tracer;
        private DateTime _lastActivity;

        public IdleManager(TimeSpan idleTimeout, ITracer tracer)
        {
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
            var processWrapper = new ProcessWrapper(process);
            WaitForExit(processWrapper);
        }

        internal void WaitForExit(IProcess process)
        {
            DateTime lastActivity = _lastActivity;
            DateTime lastCpuActivity = _lastActivity;
            TimeSpan previousCpuUsage = _initialCpuUsage;

            while (!process.WaitForExit(_idleTimeout))
            {
                // there is IO activity, continue to wait.
                if (lastActivity != _lastActivity)
                {
                    // there is io activity, reset cpu to initial
                    previousCpuUsage = _initialCpuUsage;
                    lastActivity = _lastActivity;
                    continue;
                }

                // No output activity in the past WaitIntervalTimeSpan
                TimeSpan idleTime = DateTime.UtcNow - lastActivity;

                // There wasn't any IO activity. Check if we had any CPU activity
                TimeSpan currentCpuUsage = process.GetTotalProcessorTime(_tracer);

                _tracer.Trace("{0}: no io activity for {1:0}s, prev-cpu={2:0.000}s, current-cpu={3:0.000}s", 
                    process.Name, 
                    idleTime.TotalSeconds,
                    previousCpuUsage.TotalSeconds,
                    currentCpuUsage.TotalSeconds);

                if (currentCpuUsage != previousCpuUsage)
                {
                    // The process performed some compute bound operation.  We'll wait for it some more
                    lastCpuActivity = DateTime.UtcNow;
                    previousCpuUsage = currentCpuUsage;
                    continue;
                }

                // It's likely that the process is idling waiting for user input. Kill it
                process.Kill(_tracer);

                throw CreateIdleTimeoutException(process, DateTime.UtcNow - lastCpuActivity); 
            }

            process.WaitUntilEOF();
        }

        private static Exception CreateIdleTimeoutException(IProcess process, TimeSpan totalWaitDuration)
        {
            string arguments = (process.Arguments ?? String.Empty).Trim();
            if (arguments.Length > 15)
            {
                arguments = arguments.Substring(0, 15) + " ...";
            }
            string message = String.Format(Resources.Error_ProcessAborted,
                                process.Name + " " + arguments, 
                                totalWaitDuration.TotalSeconds, 
                                SettingsKeys.CommandIdleTimeout);
            return new CommandLineException(process.Name, process.Arguments, message)
            {
                ExitCode = -1,
                Output = message,
                Error = message
            };
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    public class IdleManager
    {
        private static readonly TimeSpan _initialCpuUsage = TimeSpan.FromSeconds(-1);

        private readonly TimeSpan _idleTimeout;
        private readonly ITracer _tracer;
        private DateTime _lastActivity;
        private Stream _output;

        public IdleManager(TimeSpan idleTimeout, ITracer tracer, Stream output = null)
        {
            _idleTimeout = idleTimeout;
            _tracer = tracer;
            _output = output;
            _lastActivity = DateTime.UtcNow;
        }

        public DateTime LastActivity
        {
            get { return _lastActivity; }
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

            while (!process.WaitForExit(TimeSpan.FromSeconds(1)))
            {
                // there is IO activity, continue to wait.
                if (lastActivity != _lastActivity)
                {
                    // there is io activity, reset cpu to initial
                    previousCpuUsage = _initialCpuUsage;
                    lastActivity = _lastActivity;
                    continue;
                }

                // Check how long it's been since the last IO activity
                TimeSpan idleTime = DateTime.UtcNow - lastActivity;

                // If less than the timeout, do nothing
                if (idleTime < _idleTimeout) continue;

                // Write progress to prevent client timeouts
                WriteProgress();

                // There wasn't any IO activity. Check if we had any CPU activity

                // Check how long it's been since the last CPU activity
                TimeSpan cpuIdleTime = DateTime.UtcNow - lastCpuActivity;

                // If less than the timeout, do nothing
                if (cpuIdleTime < _idleTimeout) continue;

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

                throw CreateIdleTimeoutException(process, cpuIdleTime);
            }

            process.WaitUntilEOF();
        }

        private static byte[] progressLine = { (byte)'0', (byte)'0', (byte)'0', (byte)'6', 0x2, (byte)'.' };

        private void WriteProgress()
        {
            if (_output != null)
            {
                try
                {
                    // To output progress, we need to use git's progress sideband. Needs to look like:
                    //      0006[0x2].
                    // Where [0x2] is just an ASCII 2 char, and is the marker for the progress sideband
                    // 0006 is the line length.
                    _output.WriteAsync(progressLine, 0, progressLine.Length).Wait();

                    _output.Flush();
                }
                catch (Exception e)
                {
                    // To be defensive, if anything goes wrong during progress writing, so trying
                    _output = null;

                    _tracer.Trace("Failed to write progress in IdleManager. {0}", e.Message);
                }
            }
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
                                SettingsKeys.CommandIdleTimeout,
                                SettingsKeys.WebJobsIdleTimeoutInSeconds);
            return new CommandLineException(process.Name, process.Arguments, message)
            {
                ExitCode = -1,
                Output = message,
                Error = message
            };
        }
    }
}

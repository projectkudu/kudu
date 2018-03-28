using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using System.Net.Sockets;
using System.Net;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobRunner : BaseJobRunner, IDisposable
    {
        public const string WebsiteSCMAlwaysOnEnabledKey = "WEBSITE_SCM_ALWAYS_ON_ENABLED";
        private const int DefaultContinuousJobStoppingWaitTimeInSeconds = 5;

        private static readonly TimeSpan WarmupTimeSpan = TimeSpan.FromMinutes(2);

        private readonly LockFile _singletonLock;

        private int _started = 0;
        private Thread _continuousJobThread;
        private ContinuousJobLogger _continuousJobLogger;
        private readonly string _disableFilePath;
        private readonly IAnalytics _analytics;
        private bool _alwaysOnWarningLogged;
        private bool? _isSingleton;

        public ContinuousJobRunner(ContinuousJob continuousJob, IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
            : base(continuousJob.Name, Constants.ContinuousPath, environment, settings, traceFactory, analytics)
        {
            _analytics = analytics;
            _continuousJobLogger = new ContinuousJobLogger(continuousJob.Name, Environment, TraceFactory);
            _continuousJobLogger.RolledLogFile += OnLogFileRolled;

            _disableFilePath = Path.Combine(continuousJob.JobBinariesRootPath, "disable.job");

            _singletonLock = new LockFile(Path.Combine(JobDataPath, "singleton.job.lock"), TraceFactory, ensureLock: true);
        }

        private void UpdateStatusIfChanged(ContinuousJobStatus continuousJobStatus)
        {
            var currentStatus = _continuousJobLogger.GetStatus<ContinuousJobStatus>();
            if (!continuousJobStatus.Equals(currentStatus))
            {
                _continuousJobLogger.ReportStatus(continuousJobStatus);
            }
        }

        protected override string JobEnvironmentKeyPrefix
        {
            get { return "WEBSITE_ALWAYS_ON_JOB_RUNNING_"; }
        }

        protected override TimeSpan IdleTimeout
        {
            get { return TimeSpan.MaxValue; }
        }

        internal int WebJobPort { get; private set; }

        private void StartJob(ContinuousJob continuousJob)
        {
            // Do not go further if already started or job is disabled
            if (IsDisabled)
            {
                UpdateStatusIfChanged(ContinuousJobStatus.Stopped);
                return;
            }

            if (Interlocked.Exchange(ref _started, 1) == 1)
            {
                return;
            }

            _continuousJobLogger.ReportStatus(ContinuousJobStatus.Starting);

            CheckAlwaysOn();

            _continuousJobThread = new Thread(() =>
            {
                var threadAborted = false;
                while (!threadAborted && _started == 1 && !IsDisabled)
                {
                    try
                    {
                        // Try getting the singleton lock if single is enabled
                        bool acquired;
                        if (!TryGetLockIfSingleton(out acquired))
                        {
                            // Wait 5 seconds and retry to take the lock
                            WaitForTimeOrStop(TimeSpan.FromSeconds(5));
                            continue;
                        }

                        try
                        {
                            Stopwatch liveStopwatch = Stopwatch.StartNew();

                            _continuousJobLogger.StartingNewRun();

                            var tracer = TraceFactory.GetTracer();
                            using (tracer.Step("Run {0} {1}", continuousJob.JobType, continuousJob.Name))
                            using (new Timer(LogStillRunning, null, TimeSpan.FromHours(1), TimeSpan.FromHours(12)))
                            {
                                InitializeJobInstance(continuousJob, _continuousJobLogger);
                                WebJobPort = GetAvailableJobPort();
                                RunJobInstance(continuousJob, _continuousJobLogger, String.Empty, String.Empty, tracer, WebJobPort);
                            }

                            if (_started == 1 && !IsDisabled)
                            {
                                // The wait time between WebJob invocations is either WebJobsRestartTime (60 seconds by default) or if the WebJob
                                // Was running for at least 2 minutes there is no wait time.
                                TimeSpan webJobsRestartTime = liveStopwatch.Elapsed < WarmupTimeSpan ? Settings.GetWebJobsRestartTime() : TimeSpan.Zero;
                                _continuousJobLogger.LogInformation("Process went down, waiting for {0} seconds".FormatInvariant(webJobsRestartTime.TotalSeconds));
                                _continuousJobLogger.ReportStatus(ContinuousJobStatus.PendingRestart);
                                WaitForTimeOrStop(webJobsRestartTime);
                            }
                        }
                        finally
                        {
                            if (acquired)
                            {
                                // Make sure lock is released before re-iterating and trying to get the lock again
                                ReleaseSingletonLock();
                            }
                        }
                    }
                    catch (ThreadAbortException ex)
                    {
                        // by nature, ThreadAbortException will be rethrown at the end of this catch block and
                        // this bool may not be neccessary since while loop will be exited anyway.  we added
                        // it to be explicit.
                        threadAborted = true;

                        if (!ex.AbortedByKudu())
                        {
                            TraceFactory.GetTracer().TraceWarning("Thread was aborted, make sure WebJob was about to stop.");
                        }
                    }
                    catch (Exception ex)
                    {
                        _analytics.UnexpectedException(ex, trace: true);

                        // sleep to avoid tight exception loop
                        WaitForTimeOrStop(TimeSpan.FromSeconds(60));
                    }
                }
            });

            _continuousJobThread.Start();
        }

        /// <summary>
        /// Always On is considered disabled IFF the environment setting is there
        /// and is set to 0.
        /// </summary>
        internal static bool AlwaysOnNotEnabled()
        {
            string value = System.Environment.GetEnvironmentVariable(WebsiteSCMAlwaysOnEnabledKey);
            if (value == null)
            {
                // taking into account Non-Azure scenarios as well, where the setting
                // won't be there
                return false;
            }
            return string.Compare("0", value, StringComparison.OrdinalIgnoreCase) == 0;
        }

        /// <summary>
        /// If Always On is not enabled, log a warning to the job log on startup.
        /// </summary>
        internal void CheckAlwaysOn()
        {
            if (AlwaysOnNotEnabled() && !_alwaysOnWarningLogged)
            {
                _continuousJobLogger.LogWarning(Resources.Warning_EnableAlwaysOn);

                // we don't want to pollute the logs with this
                _alwaysOnWarningLogged = true;
            }
        }

        internal void OnLogFileRolled()
        {
            // We only want to log the warning once per log file. When
            // we move to a new log file, we want to write the warning
            // if required
            _alwaysOnWarningLogged = false;
            CheckAlwaysOn();
        }

        private void LogStillRunning(object state)
        {
            try
            {
                LogInformation("WebJob is still running");
            }
            catch
            {
                // Ignore as this is a best effort call
            }
        }

        private bool TryGetLockIfSingleton(out bool acquired)
        {
            acquired = false;

            bool isSingleton = JobSettings.IsSingleton;
            if (_isSingleton == null || _isSingleton.Value != isSingleton)
            {
                if (_isSingleton == null)
                {
                    LogInformation("WebJob singleton setting is {0}", isSingleton);
                }
                else
                {
                    LogInformation("WebJob singleton setting changed from {0} to {1}", _isSingleton.Value, isSingleton);
                }
                _isSingleton = isSingleton;
            }

            if (!isSingleton)
            {
                return true;
            }

            if (_singletonLock.Lock("Acquiring continuous WebJob singleton lock"))
            {
                acquired = true;
                LogInformation("WebJob singleton lock is acquired");
                return true;
            }

            UpdateStatusIfChanged(ContinuousJobStatus.InactiveInstance);

            return false;
        }

        public void StopJob(bool isShutdown = false)
        {
            Interlocked.Exchange(ref _started, 0);

            bool logStopped = false;

            if (_continuousJobThread != null)
            {
                logStopped = true;

                if (isShutdown)
                {
                    LogInformation("WebJob is stopping due to website shutting down");
                }

                _continuousJobLogger.ReportStatus(ContinuousJobStatus.Stopping);

                NotifyShutdownJob();

                // By default give the continuous job 5 seconds before killing it (after notifying the continuous job)
                if (!_continuousJobThread.Join(JobSettings.GetStoppingWaitTime(DefaultContinuousJobStoppingWaitTimeInSeconds)))
                {
                    _continuousJobThread.KuduAbort(String.Format("Stopping {0} {1} job", JobName, Constants.ContinuousPath));
                }

                _continuousJobThread = null;
            }

            SafeKillAllRunningJobInstances(_continuousJobLogger);

            if (logStopped)
            {
                UpdateStatusIfChanged(ContinuousJobStatus.Stopped);
            }
        }

        public void RefreshJob(ContinuousJob continuousJob, JobSettings jobSettings, bool logRefresh)
        {
            if (logRefresh)
            {
                _continuousJobLogger.LogInformation("Detected WebJob file/s were updated, refreshing WebJob");
            }

            StopJob();
            JobSettings = jobSettings;
            StartJob(continuousJob);
        }

        public void DisableJob()
        {
            OperationManager.Attempt(() => FileSystemHelpers.WriteAllBytes(_disableFilePath, new byte[0]));
            _continuousJobLogger.ReportStatus(ContinuousJobStatus.Disabling);
        }

        public void EnableJob()
        {
            OperationManager.Attempt(() => FileSystemHelpers.DeleteFile(_disableFilePath));
        }

        protected override void UpdateStatus(IJobLogger logger, string status)
        {
            logger.ReportStatus(new ContinuousJobStatus() { Status = status });
        }

        private void LogInformation(string format, params object[] args)
        {
            var message = string.Format(format, args);
            _analytics.JobEvent(JobName, message, Constants.ContinuousPath, string.Empty);
            _continuousJobLogger.LogInformation(message);
        }

        private void WaitForTimeOrStop(TimeSpan timeSpan)
        {
            var stopwatch = Stopwatch.StartNew();

            while (stopwatch.Elapsed < timeSpan && _started == 1)
            {
                Thread.Sleep(200);
            }
        }

        private bool IsDisabled
        {
            get { return OperationManager.Attempt(() => FileSystemHelpers.FileExists(_disableFilePath) || Settings.IsWebJobsStopped()); }
        }

        private void ReleaseSingletonLock()
        {
            _singletonLock.Release();

            LogInformation("WebJob singleton lock is released");
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
                if (_continuousJobThread != null)
                {
                    _continuousJobThread.KuduAbort(String.Format("Dispoing {0} {1} job", JobName, Constants.ContinuousPath));
                    _continuousJobThread = null;
                }

                if (_continuousJobLogger != null)
                {
                    _continuousJobLogger.Dispose();
                    _continuousJobLogger = null;
                }
            }
        }

        private static int GetAvailableJobPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }
    }
}

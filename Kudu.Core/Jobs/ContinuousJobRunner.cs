using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobRunner : BaseJobRunner, IDisposable
    {
        private const int DefaultContinuousJobStoppingWaitTimeInSeconds = 5;

        private static readonly TimeSpan WarmupTimeSpan = TimeSpan.FromMinutes(2);

        private readonly LockFile _singletonLock;

        private int _started = 0;
        private Thread _continuousJobThread;
        private ContinuousJobLogger _continuousJobLogger;
        private JobSettings _jobSettings;
        private readonly string _disableFilePath;

        public ContinuousJobRunner(ContinuousJob continuousJob, IEnvironment environment, IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
            : base(continuousJob.Name, Constants.ContinuousPath, environment, settings, traceFactory, analytics)
        {
            _continuousJobLogger = new ContinuousJobLogger(continuousJob.Name, Environment, TraceFactory);

            _disableFilePath = Path.Combine(continuousJob.JobBinariesRootPath, "disable.job");

            _singletonLock = new LockFile(Path.Combine(JobDataPath, "singleton.job.lock"), TraceFactory);
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

            _continuousJobThread = new Thread(() =>
            {
                try
                {
                    while (_started == 1 && !IsDisabled)
                    {
                        // Try getting the singleton lock if single is enabled
                        if (!TryGetLockIfSingleton())
                        {
                            // Wait 5 seconds and retry to take the lock
                            WaitForTimeOrStop(TimeSpan.FromSeconds(5));
                            continue;
                        }

                        Stopwatch liveStopwatch = Stopwatch.StartNew();

                        _continuousJobLogger.StartingNewRun();

                        using (new Timer(LogStillRunning, null, TimeSpan.FromHours(1), TimeSpan.FromHours(12)))
                        {
                            InitializeJobInstance(continuousJob, _continuousJobLogger);
                            RunJobInstance(continuousJob, _continuousJobLogger, String.Empty);
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

                        // Make sure lock is released before re-iterating and trying to get the lock again
                        ReleaseSingletonLock();
                    }
                }
                catch (ThreadAbortException)
                {
                    TraceFactory.GetTracer().TraceWarning("Thread was aborted, make sure WebJob was about to stop.");
                }
                catch (Exception ex)
                {
                    TraceFactory.GetTracer().TraceError(ex);
                }
                finally
                {
                    ReleaseSingletonLock();
                }
            });

            _continuousJobThread.Start();
        }

        private void LogStillRunning(object state)
        {
            try
            {
                _continuousJobLogger.LogInformation("WebJob is still running");
            }
            catch
            {
                // Ignore as this is a best effort call
            }
        }

        private bool TryGetLockIfSingleton()
        {
            bool isSingleton = _jobSettings.IsSingleton;
            if (!isSingleton)
            {
                return true;
            }

            if (_singletonLock.Lock())
            {
                return true;
            }

            _continuousJobLogger.ReportStatus(ContinuousJobStatus.InactiveInstance);
            return false;
        }

        public void StopJob()
        {
            Interlocked.Exchange(ref _started, 0);

            bool logStopped = false;

            if (_continuousJobThread != null)
            {
                logStopped = true;

                _continuousJobLogger.ReportStatus(ContinuousJobStatus.Stopping);

                NotifyShutdownJob();

                // By default give the continuous job 5 seconds before killing it (after notifying the continuous job)
                if (!_continuousJobThread.Join(_jobSettings.GetStoppingWaitTime(DefaultContinuousJobStoppingWaitTimeInSeconds)))
                {
                    _continuousJobThread.Abort();
                }

                _continuousJobThread = null;
            }

            SafeKillAllRunningJobInstances(_continuousJobLogger);

            if (logStopped)
            {
                UpdateStatusIfChanged(ContinuousJobStatus.Stopped);
            }
        }

        public void RefreshJob(ContinuousJob continuousJob, JobSettings jobSettings)
        {
            StopJob();
            _jobSettings = jobSettings;
            StartJob(continuousJob);
        }

        public void DisableJob()
        {
            OperationManager.Attempt(() => FileSystemHelpers.WriteAllBytes(_disableFilePath, new byte[0]));
            _continuousJobLogger.ReportStatus(ContinuousJobStatus.Disabling);
            StopJob();
        }

        public void EnableJob(ContinuousJob continuousJob)
        {
            OperationManager.Attempt(() => FileSystemHelpers.DeleteFile(_disableFilePath));
            StartJob(continuousJob);
        }

        protected override void UpdateStatus(IJobLogger logger, string status)
        {
            logger.ReportStatus(new ContinuousJobStatus() { Status = status });
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
            get { return FileSystemHelpers.FileExists(_disableFilePath) || Settings.IsWebJobsStopped(); }
        }

        private void ReleaseSingletonLock()
        {
            _singletonLock.Release();
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
                if (_continuousJobLogger != null)
                {
                    _continuousJobLogger.Dispose();
                    _continuousJobLogger = null;
                }
            }
        }
    }
}

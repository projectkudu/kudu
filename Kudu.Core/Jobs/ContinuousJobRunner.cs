using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobRunner : BaseJobRunner, IDisposable
    {
        private int _started = 0;
        private Thread _continuousJobThread;
        private ContinuousJobLogger _continuousJobLogger;
        private IDisposable _singletonLock;

        private readonly string _disableFilePath;
        private readonly string _singletonFilePath;

        public ContinuousJobRunner(string jobName, IEnvironment environment, IFileSystem fileSystem, IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
            : base(jobName, Constants.ContinuousPath, environment, fileSystem, settings, traceFactory, analytics)
        {
            _continuousJobLogger = new ContinuousJobLogger(jobName, Environment, FileSystem, TraceFactory);

            _disableFilePath = Path.Combine(JobBinariesPath, "disable.job");
            _singletonFilePath = Path.Combine(JobBinariesPath, "singleton.job");
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

                        InitializeJobInstance(continuousJob, _continuousJobLogger);
                        RunJobInstance(continuousJob, _continuousJobLogger);

                        if (_started == 1 && !IsDisabled)
                        {
                            TimeSpan jobsInterval = Settings.GetJobsInterval();
                            _continuousJobLogger.LogInformation("Process went down, waiting for {0} seconds".FormatInvariant(jobsInterval.TotalSeconds));
                            _continuousJobLogger.ReportStatus(ContinuousJobStatus.PendingRestart);
                            WaitForTimeOrStop(jobsInterval);
                        }
                    }
                }
                catch (Exception ex)
                {
                    TraceFactory.GetTracer().TraceError(ex);
                }
                finally
                {
                    DisposeSingletonLock();
                }
            });

            _continuousJobThread.Start();
        }

        private bool TryGetLockIfSingleton()
        {
            if (!IsSingletonFileExists())
            {
                return true;
            }

            try
            {
                if (_singletonLock == null)
                {
                    _singletonLock = FileSystem.File.Open(_singletonFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read | FileShare.Delete);
                }

                return true;
            }
            catch
            {
                _continuousJobLogger.ReportStatus(ContinuousJobStatus.InactiveInstance);
                return false;
            }
        }

        private bool IsSingletonFileExists()
        {
            return OperationManager.Attempt(() => FileSystem.File.Exists(_singletonFilePath));
        }

        public void StopJob()
        {
            Interlocked.Exchange(ref _started, 0);
            SafeKillAllRunningJobInstances(_continuousJobLogger);

            if (_continuousJobThread != null)
            {
                if (!_continuousJobThread.Join(TimeSpan.FromMinutes(1)))
                {
                    _continuousJobThread.Abort();
                }

                _continuousJobThread = null;
                _continuousJobLogger.ReportStatus(ContinuousJobStatus.Stopped);
            }
        }

        public void RefreshJob(ContinuousJob continuousJob)
        {
            StopJob();
            StartJob(continuousJob);
        }

        public void DisableJob()
        {
            OperationManager.Attempt(() => FileSystem.File.WriteAllBytes(_disableFilePath, new byte[0]));
            StopJob();
        }

        public void EnableJob(ContinuousJob continuousJob)
        {
            OperationManager.Attempt(() => FileSystem.File.Delete(_disableFilePath));
            StartJob(continuousJob);
        }

        public void SetSingleton(bool isSingleton)
        {
            if (isSingleton)
            {
                if (!IsSingletonFileExists())
                {
                    OperationManager.Attempt(() => FileSystem.File.WriteAllBytes(_singletonFilePath, new byte[0]));
                }
            }
            else
            {
                OperationManager.Attempt(() => FileSystem.File.Delete(_singletonFilePath));
            }
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
            get { return FileSystem.File.Exists(_disableFilePath); }
        }

        private void DisposeSingletonLock()
        {
            if (_singletonLock != null)
            {
                _singletonLock.Dispose();
                _singletonLock = null;
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
                if (_continuousJobLogger != null)
                {
                    _continuousJobLogger.Dispose();
                    _continuousJobLogger = null;
                }

                DisposeSingletonLock();
            }
        }
    }
}
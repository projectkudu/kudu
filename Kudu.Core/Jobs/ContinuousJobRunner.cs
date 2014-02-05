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
using Newtonsoft.Json;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobRunner : BaseJobRunner, IDisposable
    {
        private readonly LockFile _singletonLock;

        private int _started = 0;
        private Thread _continuousJobThread;
        private ContinuousJobLogger _continuousJobLogger;
        private ContinuousJobSettings _jobSettings;

        private readonly string _disableFilePath;

        public ContinuousJobRunner(string jobName, IEnvironment environment, IFileSystem fileSystem, IDeploymentSettingsManager settings, ITraceFactory traceFactory, IAnalytics analytics)
            : base(jobName, Constants.ContinuousPath, environment, fileSystem, settings, traceFactory, analytics)
        {
            _continuousJobLogger = new ContinuousJobLogger(jobName, Environment, FileSystem, TraceFactory);

            _disableFilePath = Path.Combine(JobBinariesPath, "disable.job");

            _singletonLock = new LockFile(Path.Combine(JobDataPath, "singleton.job.lock"), TraceFactory, FileSystem);

            UpdateJobSettings();
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

            UpdateJobSettings();

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

                        _continuousJobLogger.StartingNewRun();

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
                    ReleaseSingletonLock();
                }
            });

            _continuousJobThread.Start();
        }

        private void UpdateJobSettings()
        {
            try
            {
                OperationManager.Attempt(() =>
                {
                    _jobSettings = GetJobSettings();
                });
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
            }
        }

        private bool TryGetLockIfSingleton()
        {
            if (_jobSettings.IsSingleton == false)
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
            SafeKillAllRunningJobInstances(_continuousJobLogger);

            if (_continuousJobThread != null)
            {
                _continuousJobLogger.ReportStatus(ContinuousJobStatus.Stopping);

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
            _continuousJobLogger.ReportStatus(ContinuousJobStatus.Disabling);
            StopJob();
        }

        public void EnableJob(ContinuousJob continuousJob)
        {
            OperationManager.Attempt(() => FileSystem.File.Delete(_disableFilePath));
            StartJob(continuousJob);
        }

        public ContinuousJobSettings GetJobSettings()
        {
            var jobSettingsPath = GetJobSettingsPath();
            if (!FileSystem.File.Exists(jobSettingsPath))
            {
                return new ContinuousJobSettings();
            }

            string jobSettingsContent = FileSystem.File.ReadAllText(jobSettingsPath);
            return JsonConvert.DeserializeObject<ContinuousJobSettings>(jobSettingsContent);
        }

        public void SetJobSettings(ContinuousJobSettings jobSettings)
        {
            var jobSettingsPath = GetJobSettingsPath();
            string jobSettingsContent = JsonConvert.SerializeObject(jobSettings);
            FileSystem.File.WriteAllText(jobSettingsPath, jobSettingsContent);
        }

        private string GetJobSettingsPath()
        {
            return Path.Combine(JobBinariesPath, "settings.job");
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

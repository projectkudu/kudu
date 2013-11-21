using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobsManager : JobsManagerBase<ContinuousJob>, IContinuousJobsManager, IDisposable
    {
        private const int TimeoutUntilMakingChanges = 5 * 1000;
        private const int CheckForWatcherTimeout = 30 * 1000;

        private readonly Dictionary<string, ContinuousJobRunner> _continuousJobRunners = new Dictionary<string, ContinuousJobRunner>(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> _updatedJobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lockObject = new object();

        private Timer _makeChangesTimer;
        private Timer _startFileWatcherTimer;
        private FileSystemWatcher _fileSystemWatcher;

        private bool _makingChanges;

        public ContinuousJobsManager(ITraceFactory traceFactory, IEnvironment environment, IFileSystem fileSystem, IDeploymentSettingsManager settings, IAnalytics analytics)
            : base(traceFactory, environment, fileSystem, settings, analytics, Constants.ContinuousPath)
        {
            _makeChangesTimer = new Timer(OnMakeChanges);
            _startFileWatcherTimer = new Timer(StartWatcher);
            _startFileWatcherTimer.Change(0, Timeout.Infinite);
        }

        public override IEnumerable<ContinuousJob> ListJobs()
        {
            return ListJobsInternal();
        }

        public override ContinuousJob GetJob(string jobName)
        {
            return GetJobInternal(jobName);
        }

        public void DisableJob(string jobName)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(jobName, out continuousJobRunner))
            {
                throw new JobNotFoundException();
            }

            continuousJobRunner.DisableJob();
        }

        public void EnableJob(string jobName)
        {
            ContinuousJob continuousJob = GetJob(jobName);
            if (continuousJob == null)
            {
                throw new JobNotFoundException();
            }

            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(continuousJob.Name, out continuousJobRunner))
            {
                throw new InvalidOperationException("Missing job runner for an existing job - " + jobName);
            }

            continuousJobRunner.EnableJob(continuousJob);
        }

        protected override void UpdateJob(ContinuousJob job)
        {
            string jobsSpecificDataPath = Path.Combine(JobsDataPath, job.Name);
            FileSystemHelpers.EnsureDirectory(FileSystem, jobsSpecificDataPath);

            job.LogUrl = BuildLogUrl(job.Name);
            job.Status = GetStatus<ContinuousJobStatus>(Path.Combine(jobsSpecificDataPath, JobLogger.StatusFile)).Status ??
                            ContinuousJobStatus.Initializing.Status;
        }

        protected override Uri BuildDefaultExtraInfoUrl(string jobName)
        {
            return BuildLogUrl(jobName);
        }

        private Uri BuildLogUrl(string jobName)
        {
            return BuildVfsUrl("{0}/{1}".FormatInvariant(jobName, ContinuousJobLogger.JobLogFileName));
        }

        private void OnMakeChanges(object state)
        {
            HashSet<string> updatedJobs;

            lock (_lockObject)
            {
                if (_makingChanges)
                {
                    _makeChangesTimer.Change(TimeoutUntilMakingChanges, Timeout.Infinite);
                    return;
                }

                _makingChanges = true;

                updatedJobs = _updatedJobs;
                _updatedJobs = new HashSet<string>();
            }

            foreach (string updatedJobName in updatedJobs)
            {
                try
                {
                    ContinuousJob continuousJob = GetJob(updatedJobName);
                    if (continuousJob == null)
                    {
                        RemoveJob(updatedJobName);
                    }
                    else
                    {
                        RefreshJob(continuousJob);
                    }
                }
                catch (Exception ex)
                {
                    TraceFactory.GetTracer().TraceError(ex);
                }
            }

            _makingChanges = false;
        }

        private void RefreshJob(ContinuousJob continuousJob)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(continuousJob.Name, out continuousJobRunner))
            {
                continuousJobRunner = new ContinuousJobRunner(continuousJob.Name, Environment, FileSystem, Settings, TraceFactory, Analytics);
                _continuousJobRunners.Add(continuousJob.Name, continuousJobRunner);
            }

            continuousJobRunner.RefreshJob(continuousJob);
        }

        private void RemoveJob(string updatedJobName)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(updatedJobName, out continuousJobRunner))
            {
                return;
            }

            continuousJobRunner.StopJob();

            _continuousJobRunners.Remove(updatedJobName);
        }

        private void StartWatcher(object state)
        {
            lock (_lockObject)
            {
                // Check if there is a directory we can listen on
                if (!FileSystem.Directory.Exists(JobsBinariesPath))
                {
                    // If not check again in 30 seconds
                    _startFileWatcherTimer.Change(CheckForWatcherTimeout, Timeout.Infinite);
                    return;
                }

                // Start file system watcher
                _fileSystemWatcher = new FileSystemWatcher(JobsBinariesPath);
                _fileSystemWatcher.Created += OnChanged;
                _fileSystemWatcher.Changed += OnChanged;
                _fileSystemWatcher.Deleted += OnChanged;
                _fileSystemWatcher.Renamed += OnChanged;
                _fileSystemWatcher.Error += OnError;
                _fileSystemWatcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName | NotifyFilters.FileName |
                                                  NotifyFilters.LastWrite;
                _fileSystemWatcher.IncludeSubdirectories = true;
                _fileSystemWatcher.EnableRaisingEvents = true;

                // Refresh all jobs
                IEnumerable<ContinuousJob> continuousJobs = ListJobs();
                IEnumerable<string> continuousJobsNames = _continuousJobRunners.Keys.Union(continuousJobs.Select(j => j.Name));
                foreach (string continuousJobName in continuousJobsNames)
                {
                    MarkJobUpdated(continuousJobName);
                }
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            Exception ex = e.GetException();
            TraceFactory.GetTracer().TraceError(ex.ToString());
            ResetWatcher();
        }

        private void ResetWatcher()
        {
            DisposeWatcher();

            _startFileWatcherTimer.Change(CheckForWatcherTimeout, Timeout.Infinite);
        }

        private void DisposeWatcher()
        {
            if (_fileSystemWatcher != null)
            {
                _fileSystemWatcher.EnableRaisingEvents = false;
                _fileSystemWatcher.Dispose();
                _fileSystemWatcher = null;
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            if (path != null && path.Length > JobsBinariesPath.Length)
            {
                path = path.Substring(JobsBinariesPath.Length).TrimStart(Path.DirectorySeparatorChar);
                int firstSeparator = path.IndexOf(Path.DirectorySeparatorChar);
                if (firstSeparator > 0)
                {
                    string jobName = path.Substring(0, firstSeparator);
                    MarkJobUpdated(jobName);
                }
            }
        }

        private void MarkJobUpdated(string jobName)
        {
            _updatedJobs.Add(jobName);
            _makeChangesTimer.Change(TimeoutUntilMakingChanges, Timeout.Infinite);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            // HACK: Next if statement should be removed once ninject wlll not dispose this class
            // Since ninject automatically calls dispose we currently disable it
            if (disposing)
            {
                return;
            }
            // End of code to be removed

            if (disposing)
            {
                if (_startFileWatcherTimer != null)
                {
                    _startFileWatcherTimer.Dispose();
                    _startFileWatcherTimer = null;
                }

                DisposeWatcher();

                if (_makeChangesTimer != null)
                {
                    _makeChangesTimer.Dispose();
                    _makeChangesTimer = null;
                }
            }
        }
    }
}
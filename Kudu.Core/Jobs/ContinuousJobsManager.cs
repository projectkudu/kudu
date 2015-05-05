using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    public class ContinuousJobsManager : JobsManagerBase<ContinuousJob>, IContinuousJobsManager, IDisposable
    {
        private const string StatusFilesSearchPattern = ContinuousJobStatus.FileNamePrefix + "*";

        private const int TimeoutUntilMakingChanges = 5 * 1000;
        private const int CheckForWatcherTimeout = 30 * 1000;

        private readonly Dictionary<string, ContinuousJobRunner> _continuousJobRunners = new Dictionary<string, ContinuousJobRunner>(StringComparer.OrdinalIgnoreCase);

        private HashSet<string> _updatedJobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lockObject = new object();

        private Timer _makeChangesTimer;
        private Timer _startFileWatcherTimer;
        private FileSystemWatcher _fileSystemWatcher;

        private bool _makingChanges;

        private bool _firstTimeMakingChanges = true;

        public ContinuousJobsManager(ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics)
            : base(traceFactory, environment, settings, analytics, Constants.ContinuousPath)
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
            var continuousJobRunner = GetJobRunner(jobName);
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

            continuousJobRunner.EnableJob();
        }

        private ContinuousJobRunner GetJobRunner(string jobName)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(jobName, out continuousJobRunner))
            {
                throw new JobNotFoundException();
            }
            return continuousJobRunner;
        }

        protected override void OnShutdown()
        {
            lock (_lockObject)
            {
                StopWatcher();

                foreach (ContinuousJobRunner continuousJobRunner in _continuousJobRunners.Values)
                {
                    continuousJobRunner.StopJob(isShutdown: true);
                }
            }
        }

        protected override void UpdateJob(ContinuousJob job)
        {
            string jobsSpecificDataPath = Path.Combine(JobsDataPath, job.Name);
            FileSystemHelpers.EnsureDirectory(jobsSpecificDataPath);

            job.LogUrl = BuildLogUrl(job.Name);
            UpdateDetailedStatus(job, jobsSpecificDataPath);
        }

        /// <summary>
        /// Update the status and the detailed status of the job.
        /// The status is the status of one of the instances,
        /// The detailed status contains status for all instances and looks like:
        ///
        /// aabbcc - Running
        /// 112233 - PendingRestart
        ///
        /// </summary>
        private void UpdateDetailedStatus(ContinuousJob job, string jobsSpecificDataPath)
        {
            string instanceId = InstanceIdUtility.GetShortInstanceId();

            string[] statusFiles = FileSystemHelpers.GetFiles(jobsSpecificDataPath, StatusFilesSearchPattern);
            if (statusFiles.Length <= 0)
            {
                // If no status files exist update to default values
                string status = ContinuousJobStatus.Initializing.Status;

                job.DetailedStatus = instanceId + " - " + status;
                job.Status = status;

                return;
            }

            string lastStatus = null;
            var stringBuilder = new StringBuilder();

            foreach (string statusFile in statusFiles)
            {
                // Extract instance id from the file name
                int statusFileNameIndex = statusFile.IndexOf(ContinuousJobStatus.FileNamePrefix, StringComparison.OrdinalIgnoreCase);
                string statusFileInstanceId = statusFile.Substring(statusFileNameIndex + ContinuousJobStatus.FileNamePrefix.Length);

                // Try to delete file, it'll be deleted if no one holds a lock on it
                // That way we know the status file is obsolete
                if (String.Equals(statusFileInstanceId, instanceId, StringComparison.OrdinalIgnoreCase)
                    || !TryDelete(statusFile))
                {
                    // If we couldn't delete the file, we know it holds the status of an actual instance holding it
                    var continuousJobStatus = GetStatus<ContinuousJobStatus>(statusFile) ?? ContinuousJobStatus.Initializing;

                    stringBuilder.AppendLine(statusFileInstanceId + " - " + continuousJobStatus.Status);
                    if (lastStatus == null ||
                        !ContinuousJobStatus.InactiveInstance.Equals(continuousJobStatus))
                    {
                        lastStatus = continuousJobStatus.Status;
                    }
                }
            }

            // Job status will only show one of the statuses
            job.Status = lastStatus;
            job.DetailedStatus = stringBuilder.ToString();
        }

        private static bool TryDelete(string statusFile)
        {
            try
            {
                FileSystemHelpers.DeleteFileSafe(statusFile);
                return true;
            }
            catch
            {
                return false;
            }
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
                    if (continuousJob == null || !String.IsNullOrEmpty(continuousJob.Error))
                    {
                        RemoveJob(updatedJobName);
                    }
                    else
                    {
                        RefreshJob(continuousJob, logRefresh: !_firstTimeMakingChanges);
                    }
                }
                catch (Exception ex)
                {
                    TraceFactory.GetTracer().TraceError(ex);
                }
            }

            _makingChanges = false;
            _firstTimeMakingChanges = false;
        }

        private void RefreshJob(ContinuousJob continuousJob, bool logRefresh)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(continuousJob.Name, out continuousJobRunner))
            {
                continuousJobRunner = new ContinuousJobRunner(continuousJob, Environment, Settings, TraceFactory, Analytics);
                _continuousJobRunners.Add(continuousJob.Name, continuousJobRunner);
            }

            JobSettings jobSettings = continuousJob.Settings;
            continuousJobRunner.RefreshJob(continuousJob, jobSettings, logRefresh);
        }

        private void RemoveJob(string updatedJobName)
        {
            ContinuousJobRunner continuousJobRunner;
            if (!_continuousJobRunners.TryGetValue(updatedJobName, out continuousJobRunner))
            {
                return;
            }

            continuousJobRunner.StopJob();
            continuousJobRunner.Dispose();

            _continuousJobRunners.Remove(updatedJobName);
        }

        private void StartWatcher(object state)
        {
            try
            {
                lock (_lockObject)
                {
                    // Check if there is a directory we can listen on
                    if (!FileSystemHelpers.DirectoryExists(JobsBinariesPath))
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
            catch (Exception ex)
            {
                Analytics.UnexpectedException(ex);

                // Retry in 30 seconds.
                _startFileWatcherTimer.Change(CheckForWatcherTimeout, Timeout.Infinite);
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

        private void StopWatcher()
        {
            DisposeWatcher();
            _startFileWatcherTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            if (path != null && path.Length > JobsBinariesPath.Length)
            {
                path = path.Substring(JobsBinariesPath.Length).TrimStart(Path.DirectorySeparatorChar);
                int firstSeparator = path.IndexOf(Path.DirectorySeparatorChar);
                string jobName;
                if (firstSeparator >= 0)
                {
                    jobName = path.Substring(0, firstSeparator);
                }
                else
                {
                    if (e.ChangeType == WatcherChangeTypes.Changed)
                    {
                        return;
                    }

                    jobName = path;
                }

                if (!String.IsNullOrWhiteSpace(jobName))
                {
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
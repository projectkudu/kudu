using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Web.Hosting;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Jobs
{
    /// <summary>
    /// Responsible for detecting when a file is changed in a job
    /// </summary>
    public class JobsFileWatcher : IDisposable
    {
        private readonly ITraceFactory _traceFactory;
        private readonly IAnalytics _analytics;
        private const int TimeoutUntilMakingChanges = 5*1000;
        private const int CheckForWatcherTimeout = 30*1000;

        private HashSet<string> _updatedJobs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        private readonly object _lockObject = new object();

        private Timer _makeChangesTimer;
        private Timer _startFileWatcherTimer;
        private FileSystemWatcher _fileSystemWatcher;
        private int _pendingOnError;

        private bool _makingChanges;
        private readonly string _watchedDirectoryPath;
        private readonly Action<string> _onJobChanged;
        private readonly string _filter;
        private readonly Func<bool, IEnumerable<string>> _listJobNames;
        private readonly string _jobType;

        public JobsFileWatcher(
            string watchedDirectoryPath,
            Action<string> onJobChanged,
            string filter,
            Func<bool, IEnumerable<string>> listJobNames,
            ITraceFactory traceFactory,
            IAnalytics analytics,
            string jobType)
        {
            _traceFactory = traceFactory;
            _analytics = analytics;
            _watchedDirectoryPath = watchedDirectoryPath;
            _onJobChanged = onJobChanged;
            _filter = filter;
            _listJobNames = listJobNames;
            _jobType = jobType;

            _makeChangesTimer = new Timer(OnMakeChanges);
            _startFileWatcherTimer = new Timer(StartWatcher);
            _startFileWatcherTimer.Change(0, Timeout.Infinite);

            FirstTimeMakingChanges = true;
        }

        public bool FirstTimeMakingChanges { get; private set; }

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
                // job initialization and changed come thru this code path
                _analytics.JobEvent(updatedJobName, "Job initializing", _jobType, String.Empty);

                try
                {
                    _onJobChanged(updatedJobName);

                    _analytics.JobEvent(updatedJobName, "Job initialization success", _jobType, String.Empty);
                }
                catch (Exception ex)
                {
                    _analytics.JobEvent(updatedJobName, "Job initialization failed", _jobType, ex.ToString());

                    _traceFactory.GetTracer().TraceError(ex);
                }
            }

            _makingChanges = false;
            FirstTimeMakingChanges = false;
        }

        private void StartWatcher(object state)
        {
            try
            {
                lock (_lockObject)
                {
                    // Check if there is a directory we can listen on
                    if (!FileSystemHelpers.DirectoryExists(_watchedDirectoryPath))
                    {
                        // If not check again in 30 seconds
                        _startFileWatcherTimer.Change(CheckForWatcherTimeout, Timeout.Infinite);
                        return;
                    }

                    // dispose existing _fileSystemWatcher in case of exception and retry
                    DisposeWatcher();

                    // Start file system watcher
                    _fileSystemWatcher = _filter != null ? new FileSystemWatcher(_watchedDirectoryPath, _filter) : new FileSystemWatcher(_watchedDirectoryPath);
                    _fileSystemWatcher.Created += OnChanged;
                    _fileSystemWatcher.Changed += OnChanged;
                    _fileSystemWatcher.Deleted += OnChanged;
                    _fileSystemWatcher.Renamed += OnChanged;
                    _fileSystemWatcher.Error += OnError;
                    _fileSystemWatcher.NotifyFilter = NotifyFilters.CreationTime | NotifyFilters.DirectoryName |
                                                      NotifyFilters.FileName |
                                                      NotifyFilters.LastWrite;
                    _fileSystemWatcher.IncludeSubdirectories = true;
                    _fileSystemWatcher.EnableRaisingEvents = true;

                    // Refresh all jobs
                    IEnumerable<string> jobNames = _listJobNames(true);
                    foreach (string jobName in jobNames)
                    {
                        MarkJobUpdated(jobName);
                    }
                }
            }
            catch (Exception ex)
            {
                _analytics.UnexpectedException(ex);

                // Retry in 30 seconds.
                _startFileWatcherTimer.Change(CheckForWatcherTimeout, Timeout.Infinite);
            }
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            // Looking at the log, we observe a burst of OnErrors in short timeframe such as ...
            // System.ComponentModel.Win32Exception (0x80004005): The semaphore timeout period has expired
            // System.ComponentModel.Win32Exception (0x80004005): The specified network name is no longer available
            // System.IO.InternalBufferOverflowException: Too many changes at once in directory: ... 
            // Many are due to storage issues - the later happened during deployment.
            // This is to avoid queue multiple work items queueing.
            if (Interlocked.Exchange(ref _pendingOnError, 1) == 1)
            {
                return;
            }

            // Error event is raised when the directory being watched gets deleted
            // in cases when a parent to that directory is deleted, this handler should 
            // finish quickly so the deletion does not fail.
            HostingEnvironment.QueueBackgroundWorkItem(cancellationToken =>
            {
                try
                {
                    Exception ex = e.GetException();

                    _analytics.UnexpectedException(ex, trace: false);

                    _traceFactory.GetTracer().TraceError(ex.ToString());
                    ResetWatcher();
                }
                finally
                {
                    Interlocked.Exchange(ref _pendingOnError, 0);
                }
            });
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

        public void Stop()
        {
            lock (_lockObject)
            {
                DisposeWatcher();
                _startFileWatcherTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void OnChanged(object sender, FileSystemEventArgs e)
        {
            string path = e.FullPath;
            if (path != null && path.Length > _watchedDirectoryPath.Length)
            {
                path = path.Substring(_watchedDirectoryPath.Length).TrimStart(Path.DirectorySeparatorChar);
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
                    _analytics.JobEvent(jobName, String.Format("FileEvent {0} {1}", e.ChangeType, e.FullPath), _jobType, String.Empty);

                    MarkJobUpdated(jobName);
                }
            }
        }

        private void MarkJobUpdated(string jobName)
        {
            lock (_lockObject)
            {
                _updatedJobs.Add(jobName);
            }

            _makeChangesTimer.Change(TimeoutUntilMakingChanges, Timeout.Infinite);
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

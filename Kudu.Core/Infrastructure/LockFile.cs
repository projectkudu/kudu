using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// A thread safe/multi application safe lock file.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "Because of a bug in Ninject we can't make this disposable as it would otherwise get disposed on every request.")]
    public class LockFile : IOperationLock
    {
        private readonly string _path;
        private readonly ITraceFactory _traceFactory;

        private ConcurrentQueue<QueueItem> _lockRequestQueue;
        private FileSystemWatcher _lockFileWatcher;

        private Stream _lockStream;

        public LockFile(string path)
            : this(path, NullTracerFactory.Instance)
        {
        }

        public LockFile(string path, ITraceFactory traceFactory)
        {
            _path = Path.GetFullPath(path);
            _traceFactory = traceFactory;
        }

        public void InitializeAsyncLocks()
        {
            _lockRequestQueue = new ConcurrentQueue<QueueItem>();

            FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(_path));

            // Set up lock file watcher. Note that depending on how the file is accessed the file watcher may generate multiple events.
            _lockFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(_path), Path.GetFileName(_path));
            _lockFileWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
            _lockFileWatcher.Changed += OnLockReleasedInternal;
            _lockFileWatcher.Deleted += OnLockReleasedInternal;
            _lockFileWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Because of a bug in Ninject in how it disposes objects in the global scope for each request
        /// we can't use Dispose to shut down the file system watcher. Otherwise this would get disposed
        /// on every request.
        /// </summary>
        public void TerminateAsyncLocks()
        {
            if (_lockFileWatcher != null)
            {
                _lockRequestQueue = null;
                _lockFileWatcher.EnableRaisingEvents = false;
                _lockFileWatcher.Dispose();
                _lockFileWatcher = null;
            }
        }

        public bool IsHeld
        {
            get
            {
                // If there's no file then there's no process holding onto it
                if (!FileSystemHelpers.FileExists(_path))
                {
                    return false;
                }

                try
                {
                    // If there is a file, lets see if someone has an open handle to it, or if it's
                    // just hanging there for no reason
                    using (FileSystemHelpers.OpenFile(_path, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
                }
                catch (Exception ex)
                {
                    TraceIfUnknown(ex);
                    return true;
                }

                // cleanup inactive lock file.  technically, it is not needed
                // we just want to see the lock folder is clean, if no active lock.
                DeleteFileSafe();

                return false;
            }
        }

        public bool Lock()
        {
            Stream lockStream = null;
            try
            {
                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(_path));

                lockStream = FileSystemHelpers.OpenFile(_path, FileMode.Create, FileAccess.Write, FileShare.Read);

                WriteLockInfo(lockStream);

                OnLockAcquired();

                _lockStream = lockStream;
                lockStream = null;

                return true;
            }
            catch (Exception ex)
            {
                TraceIfUnknown(ex);

                if (lockStream != null)
                {
                    lockStream.Close();
                }

                return false;
            }
        }

        protected virtual void OnLockAcquired()
        {
            // no-op
        }

        protected virtual void OnLockRelease()
        {
            // no-op
        }

        // we only write the lock info at lock's enter since
        // lock file will be cleaned up at release
        private static void WriteLockInfo(Stream lockStream)
        {
            var strb = new StringBuilder();
            strb.Append(DateTime.UtcNow.ToString("s"));
            strb.AppendLine(System.Environment.StackTrace);

            var bytes = Encoding.UTF8.GetBytes(strb.ToString());
            lockStream.Write(bytes, 0, bytes.Length);
            lockStream.Flush();
        }

        /// <summary>
        /// Returns a lock right away or waits asynchronously until a lock is available.
        /// </summary>
        /// <returns>Task indicating the task of acquiring the lock.</returns>
        public Task LockAsync()
        {
            if (_lockFileWatcher == null)
            {
                throw new InvalidOperationException(Resources.Error_AsyncLockNotInitialized);
            }

            // See if we can get the lock -- if not then enqueue lock request.
            if (Lock())
            {
                return Task.FromResult(true);
            }

            QueueItem item = new QueueItem();
            _lockRequestQueue.Enqueue(item);
            return item.HasLock.Task;
        }

        public void Release()
        {
            // Normally, this should never be null here, but currently some LiveScmEditorController code calls Release() incorrectly
            if (_lockStream == null)
            {
                OnLockRelease();
                return;
            }

            var temp = _lockStream;
            _lockStream = null;
            temp.Close();

            // cleanup inactive lock file.  technically, it is not needed
            // we just want to see the lock folder is clean, if no active lock.
            DeleteFileSafe();

            OnLockRelease();
        }

        // we cannot use FileSystemHelpers.DeleteFileSafe.
        // it does not handled IOException due to 'file in used'.
        private void DeleteFileSafe()
        {
            try
            {
                FileSystemHelpers.DeleteFile(_path);
            }
            catch (Exception ex)
            {
                TraceIfUnknown(ex);
            }
        }

        private void TraceIfUnknown(Exception ex)
        {
            if (!(ex is IOException) && !(ex is UnauthorizedAccessException))
            {
                // trace unexpected exception
                _traceFactory.GetTracer().TraceError(ex);
            }
        }

        /// <summary>
        /// When a lock file change has been detected we check whether there are queued up lock requests.
        /// If so then we attempt to get the lock and dequeue the next request.
        /// </summary>
        private void OnLockReleasedInternal(object sender, FileSystemEventArgs e)
        {
            if (!_lockRequestQueue.IsEmpty)
            {
                if (Lock())
                {
                    if (!_lockRequestQueue.IsEmpty)
                    {
                        QueueItem item;
                        if (!_lockRequestQueue.TryDequeue(out item))
                        {
                            string msg = String.Format(Resources.Error_AsyncLockNoLockRequest, _lockRequestQueue.Count);
                            _traceFactory.GetTracer().TraceError(msg);
                            Release();
                        }

                        if (!item.HasLock.TrySetResult(true))
                        {
                            _traceFactory.GetTracer().TraceError(Resources.Error_AsyncLockRequestCompleted);
                            Release();
                        }
                    }
                    else
                    {
                        Release();
                    }
                }
            }
        }

        private class QueueItem
        {
            public QueueItem()
            {
                HasLock = new TaskCompletionSource<bool>();
            }

            public TaskCompletionSource<bool> HasLock { get; private set; }
        }
    }
}
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// A thread safe/multi application safe lock file.
    /// </summary>
    public class LockFile : IOperationLock
    {
        private readonly string _path;
        private Stream _lockStream;
        private readonly ITraceFactory _traceFactory;
        private event Action _lockReleased;

        public LockFile(ITraceFactory traceFactory, string path)
        {
            _traceFactory = traceFactory;
            _path = Path.GetFullPath(path);
        }


        public bool IsHeld
        {
            get
            {
                try
                {
                    // If there's no file then there's no process holding onto it
                    if (!File.Exists(_path))
                    {
                        return false;
                    }

                    // If there is a file, lets see if someone has an open handle to it, or if it's
                    // just hanging there for no reason
                    using (new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        // Nobody is here
                        return false;
                    }
                }
                catch
                {
                    return true;
                }
            }
        }

        public bool Lock()
        {
            try
            {
                if (Interlocked.Exchange(ref _lockStream, new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None)) == null)
                {
                    TraceLock("Aquired Lock");
                    _lockStream.WriteByte(0);
                    return true;
                }
                return false;
            }
            catch
            {
                TraceLock("Aquire Lock failed");
                return false;
            }
        }

        public bool Release()
        {
            if (_lockStream == null)
            {
                return false;
            }

            try
            {
                _lockStream.Close();
                File.Delete(_path);

                if (_lockReleased != null)
                {
                    _lockReleased();
                }

                TraceLock("Lock released");
                return true;
            }
            catch
            {
                TraceLock("Lock release failed");
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _lockStream, null);
            }
        }

        public bool Wait(TimeSpan timeout)
        {
            if (_lockStream == null)
            {
                TraceLock("Waiting on lock");

                // Poll the file system every second
                var interval = TimeSpan.FromSeconds(1);
                var elapsed = TimeSpan.Zero;

                bool timedout = false;

                // This is less efficient than a file change nofitication but more reliable
                // as there's a race condition when setting up the notification
                while (IsHeld)
                {
                    Thread.Sleep(interval);
                    elapsed += interval;

                    if (elapsed > timeout)
                    {
                        timedout = true;
                        break;
                    }
                }

                TraceLock("Waiting complete");

                return timedout;
            }
            else
            {
                // Same instance of the lock, so we can shortcut to an event handler
                var wh = new ManualResetEventSlim();
                Action handler = null;

                handler = () =>
                {
                    wh.Set();

                    _lockReleased -= handler;
                };

                _lockReleased += handler;

                TraceLock("Waiting on lock");

                bool timedout = wh.Wait(timeout);

                TraceLock("Waiting complete");

                return timedout;
            }
        }

        private void TraceLock(string message)
        {
            ITracer tracer = _traceFactory.GetTracer();
            tracer.Trace(message, new Dictionary<string, string>
            {
                { "type", "lock" }
            });
        }
    }
}
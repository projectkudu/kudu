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
        private readonly string _directory;
        private Stream _lockStream;
        private readonly ITraceFactory _traceFactory;

        public LockFile(ITraceFactory traceFactory, string path)
        {
            _traceFactory = traceFactory;
            _path = Path.GetFullPath(path);
            _directory = Path.GetDirectoryName(_path);
        }

        public bool IsHeld
        {
            get
            {
                // If there's no file then there's no process holding onto it
                if (!File.Exists(_path))
                {
                    return false;
                }

                try
                {
                    // If there is a file, lets see if someone has an open handle to it, or if it's
                    // just hanging there for no reason
                    using (new FileStream(_path, FileMode.Open, FileAccess.Write, FileShare.None))
                    {
                        try
                        {
                            // Nobody is here, so delete the turd file
                            File.Delete(_path);
                        }
                        catch { }
                        return false;
                    }
                }
                catch(IOException)
                {
                    return true;
                }
            }
        }

        public bool Lock()
        {
            try
            {
                FileSystemHelpers.EnsureDirectory(_directory);

                if (Interlocked.Exchange(ref _lockStream, new FileStream(_path, FileMode.Create, FileAccess.Write, FileShare.None)) == null)
                {
                    _lockStream.WriteByte(0);
                    return true;
                }
                return false;
            }
            catch
            {
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

                try
                {
                    FileSystemHelpers.DeleteIfEmpty(_directory);
                }
                catch
                {
                    // Doesn't matter if this fails, we're just trying to be tidy
                }

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
            // Poll the file system every second
            var interval = TimeSpan.FromSeconds(1);
            var elapsed = TimeSpan.Zero;

            bool timedout = false;

            // This is less efficient than a file change nofitication but more reliable
            // as there's a race condition when setting up the notification
            while (IsHeld)
            {
                if (elapsed >= timeout)
                {
                    timedout = true;
                    break;
                }

                Thread.Sleep(interval);
                elapsed += interval;
            }

            return timedout;
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
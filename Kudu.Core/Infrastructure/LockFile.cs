using System;
using System.IO;
using System.IO.Abstractions;
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
        private readonly ITraceFactory _traceFactory;
        private readonly IFileSystem _fileSystem;

        private Stream _lockStream;

        public LockFile(string path)
            : this(path, NullTracerFactory.Instance, new FileSystem())
        {
        }

        public LockFile(string path, ITraceFactory traceFactory, IFileSystem fileSystem)
        {
            _path = Path.GetFullPath(path);
            _traceFactory = traceFactory;
            _fileSystem = fileSystem;

            FileSystemHelpers.EnsureDirectory(fileSystem, Path.GetDirectoryName(path));
        }

        public bool IsHeld
        {
            get
            {
                // If there's no file then there's no process holding onto it
                if (!_fileSystem.File.Exists(_path))
                {
                    return false;
                }

                try
                {
                    // If there is a file, lets see if someone has an open handle to it, or if it's
                    // just hanging there for no reason
                    using (_fileSystem.File.Open(_path, FileMode.Open, FileAccess.Write, FileShare.None)) { }
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
            try
            {
                _lockStream = _fileSystem.File.Open(_path, FileMode.Create, FileAccess.Write, FileShare.None);

                return true;
            }
            catch (Exception ex)
            {
                TraceIfUnknown(ex);

                return false;
            }
        }

        public void Release()
        {
            var temp = _lockStream;
            _lockStream = null;
            temp.Close();

            // cleanup inactive lock file.  technically, it is not needed
            // we just want to see the lock folder is clean, if no active lock.
            DeleteFileSafe();
        }

        // we cannot use FileSystemHelpers.DeleteFileSafe.
        // it does not handled IOException due to 'file in used'.
        private void DeleteFileSafe()
        {
            try
            {
                _fileSystem.File.Delete(_path);
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
    }
}
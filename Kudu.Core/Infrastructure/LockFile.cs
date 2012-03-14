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

        public LockFile(ITraceFactory traceFactory, string path)
        {
            _traceFactory = traceFactory;
            _path = path;
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

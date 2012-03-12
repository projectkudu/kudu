using System.IO;
using System.Threading;
using Kudu.Contracts.Infrastructure;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// A thread safe/multi application safe lock file.
    /// </summary>
    public class LockFile : IOperationLock
    {
        private readonly string _path;
        private Stream _lockStream;

        public LockFile(string path)
        {
            _path = path;
        }

        public bool Lock()
        {
            try
            {
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
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                Interlocked.Exchange(ref _lockStream, null);
            }
        }
    }
}

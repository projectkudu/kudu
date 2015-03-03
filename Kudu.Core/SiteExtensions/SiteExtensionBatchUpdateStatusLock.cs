using System.Diagnostics.CodeAnalysis;
using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.SiteExtensions
{
    [SuppressMessage("Microsoft.Design", "CA1052:StaticHolderTypesShouldBeSealed", Justification = "Create a subclass of LockFile with private constructor")]
    public class SiteExtensionBatchUpdateStatusLock : LockFile
    {
        public const string LockNameSuffix = "batch.lock";

        private SiteExtensionBatchUpdateStatusLock(string path)
            : base(path)
        { }

        public static SiteExtensionBatchUpdateStatusLock CreateLock(string rootPath)
        {
            string lockFilePath = Path.Combine(rootPath, LockNameSuffix);
            var batchUpdateLock = new SiteExtensionBatchUpdateStatusLock(lockFilePath);
            batchUpdateLock.InitializeAsyncLocks();
            return batchUpdateLock;
        }
    }
}

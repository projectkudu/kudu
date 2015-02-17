using System.IO;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionInstallationLock : LockFile
    {
        private string _path;
        private SiteExtensionInstallationLock(string path)
            : base(path)
        {
            _path = path;
        }

        protected override void OnLockRelease()
        {
            // if installed failed, when release lock, we should also remove empty folder as well
            base.OnLockRelease();
            try
            {
                string folder = Path.GetDirectoryName(_path);
                if (FileSystemHelpers.GetFiles(folder, "*").Length == 0)
                {
                    FileSystemHelpers.DeleteDirectorySafe(folder);
                }
            }
            catch
            {
                // no-op
            }
        }

        /// <summary>
        /// <para>Will create a file lock like this: {rootPath}/{site extension id}/install.lock</para>
        /// <para>e.g 'D:\home\site\siteextension\filecounter\install.lock'</para>
        /// </summary>
        public static SiteExtensionInstallationLock CreateLock(string rootPath, string id)
        {
            string lockFilePath = Path.Combine(rootPath, id, "install.lock");
            var installationLock = new SiteExtensionInstallationLock(lockFilePath);
            installationLock.InitializeAsyncLocks();
            return installationLock;
        }
    }
}

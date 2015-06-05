using System;
using System.IO;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.SiteExtensions
{
    public class SiteExtensionInstallationLock : LockFile
    {
        private const string LockNameSuffix = "install.lock";

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
                if (FileSystemHelpers.DirectoryExists(folder) && FileSystemHelpers.GetFiles(folder, "*").Length == 0)
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
        public static SiteExtensionInstallationLock CreateLock(string rootPath, string id, bool enableAsync = false)
        {
            string lockFilePath = Path.Combine(rootPath, id, LockNameSuffix);
            var installationLock = new SiteExtensionInstallationLock(lockFilePath);

            if (enableAsync)
            {
                installationLock.InitializeAsyncLocks();
            }

            return installationLock;
        }

        public static bool IsAnyPendingLock(string rootPath, ITracer tracer)
        {
            bool hasPendingLock = false;

            try
            {
                using (tracer.Step("Checking if there is other pending installation ..."))
                {
                    string[] packageDirs = FileSystemHelpers.GetDirectories(rootPath);
                    foreach (var dir in packageDirs)
                    {
                        string[] lockFiles = FileSystemHelpers.GetFiles(dir, string.Format("*{0}", LockNameSuffix));
                        foreach (var file in lockFiles)
                        {
                            // If there's no file then there's no process holding onto it
                            if (!FileSystemHelpers.FileExists(file))
                            {
                                continue;
                            }

                            try
                            {
                                // If there is a file, lets see if someone has an open handle to it, or if it's
                                // just hanging there for no reason
                                using (FileSystemHelpers.OpenFile(file, FileMode.Open, FileAccess.Write, FileShare.Read)) { }
                            }
                            catch
                            {
                                hasPendingLock = true;
                                break;
                            }
                        }

                        if (hasPendingLock)
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // no-op
                tracer.TraceError(ex, "Failed to check pending installation lock. Assume there is no pending lock.");
            }

            return hasPendingLock;
        }
    }
}

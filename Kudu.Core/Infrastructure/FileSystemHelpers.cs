using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Kudu.Core.Infrastructure
{
    internal static class FileSystemHelpers
    {
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static void DeleteDirectorySafe(string path, bool ignoreErrors = true)
        {
            DeleteFileSystemInfo(new DirectoryInfoWrapper(new DirectoryInfo(path)), ignoreErrors);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static void DeleteDirectoryContentsSafe(string path, bool ignoreErrors = true)
        {
            DeleteDirectoryContentsSafe(new DirectoryInfoWrapper(new DirectoryInfo(path)), ignoreErrors);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static void DeleteIfEmpty(string path)
        {
            if (!Directory.Exists(path))
            {
                return;
            }

            if (Directory.EnumerateFileSystemEntries(path).Any())
            {
                return;
            }

            // Just delete this directory
            Directory.Delete(path);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static string EnsureDirectory(string path)
        {
            return EnsureDirectory(new FileSystem(), path);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static string EnsureDirectory(IFileSystem fileSystem, string path)
        {
            if (!fileSystem.Directory.Exists(path))
            {
                fileSystem.Directory.CreateDirectory(path);
            }
            return path;
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        public static bool DeleteFileSafe(string path)
        {
            return DeleteFileSafe(new FileSystem(), path);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static bool DeleteFileSafe(IFileSystem fileSystem, string path)
        {
            try
            {
                if (fileSystem.File.Exists(path))
                {
                    fileSystem.File.Delete(path);
                    return true;
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (FileNotFoundException) { }

            return false;
        }

        private static void DeleteFileSystemInfo(FileSystemInfoBase fileSystemInfo, bool ignoreErrors)
        {
            if (!fileSystemInfo.Exists)
            {
                return;
            }

            try
            {
                fileSystemInfo.Attributes = FileAttributes.Normal;
            }
            catch
            {
                if (!ignoreErrors) throw;
            }

            var directoryInfo = fileSystemInfo as DirectoryInfoBase;

            if (directoryInfo != null)
            {
                DeleteDirectoryContentsSafe(directoryInfo, ignoreErrors);
            }

            DoSafeAction(fileSystemInfo.Delete, ignoreErrors);
        }

        private static void DeleteDirectoryContentsSafe(DirectoryInfoBase directoryInfo, bool ignoreErrors)
        {
            try
            {
                if (directoryInfo.Exists)
                {
                    foreach (var fsi in directoryInfo.GetFileSystemInfos())
                    {
                        DeleteFileSystemInfo(fsi, ignoreErrors);
                    }
                }
            }
            catch
            {
                if (!ignoreErrors) throw;
            }
        }

        private static void DoSafeAction(Action action, bool ignoreErrors)
        {
            try
            {
                OperationManager.Attempt(action);
            }
            catch
            {
                if (!ignoreErrors) throw;
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static void Copy(string sourcePath, string destinationPath, bool skipScmFolder = true)
        {
            sourcePath = Path.GetFullPath(sourcePath);
            destinationPath = Path.GetFullPath(destinationPath);

            Copy(sourcePath,
                 destinationPath,
                 new DirectoryInfoWrapper(new DirectoryInfo(sourcePath)),
                 new DirectoryInfoWrapper(new DirectoryInfo(destinationPath)),
                 path => new DirectoryInfoWrapper(new DirectoryInfo(path)),
                 skipScmFolder);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static void Copy(string sourcePath,
                                  string destinationPath,
                                  DirectoryInfoBase sourceDirectory,
                                  DirectoryInfoBase destinationDirectory,
                                  Func<string, DirectoryInfoBase> createDirectoryInfo,
                                  bool skipScmFolder)
        {
            // Skip hidden directories and directories that begin with .
            if (skipScmFolder && IsSourceControlFolder(sourceDirectory))
            {
                return;
            }

            if (!destinationDirectory.Exists)
            {
                destinationDirectory.Create();
            }

            foreach (var sourceFile in sourceDirectory.GetFiles())
            {
                string path = GetDestinationPath(sourcePath, destinationPath, sourceFile);

                sourceFile.CopyTo(path, overwrite: true);
            }


            var destDirectoryLookup = GetDirectories(destinationDirectory);
            foreach (var sourceSubDirectory in sourceDirectory.GetDirectories())
            {
                DirectoryInfoBase targetSubDirectory;
                if (!destDirectoryLookup.TryGetValue(sourceSubDirectory.Name, out targetSubDirectory))
                {
                    string path = GetDestinationPath(sourcePath, destinationPath, sourceSubDirectory);
                    targetSubDirectory = createDirectoryInfo(path);
                }

                Copy(sourcePath, destinationPath, sourceSubDirectory, targetSubDirectory, createDirectoryInfo, skipScmFolder);
            }
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static bool IsSourceControlFolder(string path)
        {
            // TODO: Update kudu sync
            return path.StartsWith(".git", StringComparison.OrdinalIgnoreCase) ||
                   path.StartsWith(".hg", StringComparison.OrdinalIgnoreCase);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static bool IsSourceControlFolder(DirectoryInfoBase directoryInfo)
        {
            return IsSourceControlFolder(directoryInfo.Name);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static string GetDestinationPath(string sourceRootPath, string destinationRootPath, FileSystemInfoBase info)
        {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static IDictionary<string, FileInfoBase> GetFiles(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetFilesWithRetry().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static IDictionary<string, DirectoryInfoBase> GetDirectories(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }

        // Call DirectoryInfoBase.GetFiles under a retry loop to make the system
        // more resilient when some files are temporarily in use
        [SuppressMessage("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode", Justification = "Method is used, misdiagnosed due to linking of this file")]
        internal static FileInfoBase[] GetFilesWithRetry(this DirectoryInfoBase info)
        {
            return OperationManager.Attempt(() =>
            {
                return info.GetFiles();
            });
        }
    }
}

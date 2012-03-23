using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;

namespace Kudu.Core.Infrastructure
{
    internal static class FileSystemHelpers
    {
        public static void DeleteDirectorySafe(string path)
        {
            DeleteFileSystemInfo(new DirectoryInfoWrapper(new DirectoryInfo(path)));
        }

        public static void DeleteDirectoryContentsSafe(string path)
        {
            DeleteDirectoryContentsSafe(new DirectoryInfoWrapper(new DirectoryInfo(path)));
        }

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

        internal static string EnsureDirectory(string path)
        {
            return EnsureDirectory(new FileSystem(), path);
        }

        internal static string EnsureDirectory(IFileSystem fileSystem, string path)
        {
            if (!fileSystem.Directory.Exists(path))
            {
                fileSystem.Directory.CreateDirectory(path);
            }
            return path;
        }

        public static void DeleteFileSafe(string path)
        {
            DeleteFileSafe(new FileSystem(), path);
        }

        internal static void DeleteFileSafe(IFileSystem fileSystem, string path)
        {
            try
            {
                if (fileSystem.File.Exists(path))
                {
                    fileSystem.File.Delete(path);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (FileNotFoundException) { }
        }

        private static void DeleteFileSystemInfo(FileSystemInfoBase fileSystemInfo)
        {
            try
            {
                if (fileSystemInfo.Exists)
                {
                    fileSystemInfo.Attributes = FileAttributes.Normal;
                }
            }
            catch
            {
            }

            var directoryInfo = fileSystemInfo as DirectoryInfoBase;

            if (directoryInfo != null)
            {
                DeleteDirectoryContentsSafe(directoryInfo);
            }

            DoSafeAction(fileSystemInfo.Delete);
        }

        private static void DeleteDirectoryContentsSafe(DirectoryInfoBase directoryInfo)
        {
            try
            {
                if (directoryInfo.Exists)
                {
                    foreach (var fsi in directoryInfo.GetFileSystemInfos())
                    {
                        DeleteFileSystemInfo(fsi);
                    }
                }
            }
            catch
            {
            }
        }

        private static void DoSafeAction(Action action)
        {
            try
            {
                OperationManager.Attempt(action);
            }
            catch
            {
            }
        }

        internal static void Copy(string sourcePath, string destinationPath, bool skipScmFolder = true)
        {
            Copy(sourcePath,
                 destinationPath,
                 new DirectoryInfoWrapper(new DirectoryInfo(sourcePath)),
                 new DirectoryInfoWrapper(new DirectoryInfo(destinationPath)),
                 path => new DirectoryInfoWrapper(new DirectoryInfo(path)),
                 skipScmFolder);
        }

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


            var destDirectoryLookup = GetDirectores(destinationDirectory);
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

        internal static bool IsSourceControlFolder(string path)
        {
            return path.StartsWith(".git", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsSourceControlFolder(DirectoryInfoBase directoryInfo)
        {
            return IsSourceControlFolder(directoryInfo.Name);
        }

        internal static string GetDestinationPath(string sourceRootPath, string destinationRootPath, FileSystemInfoBase info)
        {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        internal static IDictionary<string, FileInfoBase> GetFiles(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetFiles().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        internal static IDictionary<string, DirectoryInfoBase> GetDirectores(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}

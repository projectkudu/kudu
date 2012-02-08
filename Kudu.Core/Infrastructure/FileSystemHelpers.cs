using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;

namespace Kudu.Core.Infrastructure
{
    internal static class FileSystemHelpers
    {
        public static void SmartCopy(string sourcePath, string destinationPath, Func<string, bool> existsInPrevious, bool skipOldFiles = true)
        {
            SmartCopy(sourcePath,
                      destinationPath,
                      existsInPrevious,
                      new DirectoryInfoWrapper(new DirectoryInfo(sourcePath)),
                      new DirectoryInfoWrapper(new DirectoryInfo(destinationPath)),
                      path => new DirectoryInfoWrapper(new DirectoryInfo(path)),
                      skipOldFiles);
        }

        public static void DeleteDirectorySafe(string path)
        {
            DeleteFileSystemInfo(new DirectoryInfoWrapper(new DirectoryInfo(path)));
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
                try
                {
                    if (directoryInfo.Exists)
                    {
                        foreach (var dirInfo in directoryInfo.GetFileSystemInfos())
                        {
                            DeleteFileSystemInfo(dirInfo);
                        }
                    }
                }
                catch
                {
                }
            }

            DoSafeAction(fileSystemInfo.Delete);
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
        
        internal static void SmartCopy(string sourcePath,
                                       string destinationPath,
                                       Func<string, bool> existsInPrevious,
                                       DirectoryInfoBase sourceDirectory,
                                       DirectoryInfoBase destinationDirectory,
                                       Func<string, DirectoryInfoBase> createDirectoryInfo,
                                       bool skipOldFiles)
        {
            // Skip source control folder
            if (IsSourceControlFolder(sourceDirectory))
            {
                return;
            }

            if (!destinationDirectory.Exists)
            {
                destinationDirectory.Create();
            }

            // var previousFilesLookup = GetFiles(previousDirectory);
            var destFilesLookup = GetFiles(destinationDirectory);
            var sourceFilesLookup = GetFiles(sourceDirectory);

            foreach (var destFile in destFilesLookup.Values)
            {
                // If the file doesn't exist in the source, only delete if:
                // 1. We have no previous directory
                // 2. We have a previous directory and the file exists there

                // Trim the start path
                string previousPath = destFile.FullName.Substring(destinationPath.Length).TrimStart('\\');
                if (!sourceFilesLookup.ContainsKey(destFile.Name) &&
                    ((existsInPrevious == null) ||
                    (existsInPrevious != null &&
                    existsInPrevious(previousPath))))
                {
                    destFile.Delete();
                }
            }

            foreach (var sourceFile in sourceFilesLookup.Values)
            {
                // Skip files that start with .
                if (sourceFile.Name.StartsWith("."))
                {
                    continue;
                }

                // If a file exists in the destination then only copy it again if it's
                // last write time is greater than the same file in the source (only if it changed)
                FileInfoBase targetFile;
                if (skipOldFiles &&
                    destFilesLookup.TryGetValue(sourceFile.Name, out targetFile) &&
                    sourceFile.LastWriteTimeUtc <= targetFile.LastWriteTimeUtc)
                {
                    continue;
                }

                // Otherwise, copy the file
                string path = GetDestinationPath(sourcePath, destinationPath, sourceFile);

                sourceFile.CopyTo(path, overwrite: true);
            }

            var sourceDirectoryLookup = GetDirectores(sourceDirectory);
            var destDirectoryLookup = GetDirectores(destinationDirectory);

            foreach (var destSubDirectory in destDirectoryLookup.Values)
            {
                // If the directory doesn't exist in the source, only delete if:
                // 1. We have no previous directory
                // 2. We have a previous directory and the file exists there

                string previousPath = destSubDirectory.FullName.Substring(destinationPath.Length).TrimStart('\\');
                if (!sourceDirectoryLookup.ContainsKey(destSubDirectory.Name) &&
                    ((existsInPrevious == null) ||
                    (existsInPrevious != null &&
                    existsInPrevious(previousPath))))
                {
                    destSubDirectory.Delete(recursive: true);
                }
            }

            foreach (var sourceSubDirectory in sourceDirectoryLookup.Values)
            {
                DirectoryInfoBase targetSubDirectory;
                if (!destDirectoryLookup.TryGetValue(sourceSubDirectory.Name, out targetSubDirectory))
                {
                    string path = GetDestinationPath(sourcePath, destinationPath, sourceSubDirectory);
                    targetSubDirectory = createDirectoryInfo(path);
                }

                // Sync all sub directories
                SmartCopy(sourcePath, destinationPath, existsInPrevious, sourceSubDirectory, targetSubDirectory, createDirectoryInfo, skipOldFiles);
            }
        }

        internal static bool IsSourceControlFolder(string path)
        {
            // TODO: Add hg later
            return path.StartsWith(".git", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSourceControlFolder(DirectoryInfoBase directoryInfo)
        {
            return IsSourceControlFolder(directoryInfo.Name);
        }

        private static string GetDestinationPath(string sourceRootPath, string destinationRootPath, FileSystemInfoBase info)
        {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        private static IDictionary<string, FileInfoBase> GetFiles(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetFiles().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static IDictionary<string, DirectoryInfoBase> GetDirectores(DirectoryInfoBase info)
        {
            if (info == null)
            {
                return null;
            }
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}

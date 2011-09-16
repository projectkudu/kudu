using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;

namespace Kudu.Core.Infrastructure {
    public static class FileSystemHelpers {        
        public static void SmartCopy(string sourcePath, string destinationPath, bool skipOldFiles = true) {
            SmartCopy(sourcePath, 
                      destinationPath, 
                      new DirectoryInfoWrapper(new DirectoryInfo(sourcePath)), 
                      new DirectoryInfoWrapper(new DirectoryInfo(destinationPath)),
                      skipOldFiles);
        }

        public static void DeleteDirectorySafe(string path) {
            DeleteFileSystemInfo(new DirectoryInfoWrapper(new DirectoryInfo(path)));
        }

        public static string EnsureDirectory(string path) {
            return EnsureDirectory(new FileSystem(), path);
        }

        internal static string EnsureDirectory(IFileSystem fileSystem, string path) {
            if (!fileSystem.Directory.Exists(path)) {
                fileSystem.Directory.CreateDirectory(path);
            }
            return path;
        }
        
        public static void DeleteFileSafe(string path) {
            DeleteFileSafe(new FileSystem(), path);
        }

        internal static void DeleteFileSafe(IFileSystem fileSystem, string path) {
            try {
                if (fileSystem.File.Exists(path)) {
                    fileSystem.File.Delete(path);
                }
            }
            catch (UnauthorizedAccessException) { }
            catch (FileNotFoundException) { }
        }

        private static void DeleteFileSystemInfo(FileSystemInfoBase fileSystemInfo) {
            try {
                if (fileSystemInfo.Exists) {
                    fileSystemInfo.Attributes = FileAttributes.Normal;
                }
            }
            catch {
            }

            var directoryInfo = fileSystemInfo as DirectoryInfoBase;

            if (directoryInfo != null) {
                try {
                    if (directoryInfo.Exists) {
                        foreach (var dirInfo in directoryInfo.GetFileSystemInfos()) {
                            DeleteFileSystemInfo(dirInfo);
                        }
                    }
                }
                catch {
                }
            }

            DoSafeAction(fileSystemInfo.Delete);
        }

        private static void DoSafeAction(Action action) {
            try {
                Attempt(action);
            }
            catch {
            }
        }

        private static void Attempt(Action action, int retries = 3, int delayBeforeRetry = 250) {
            while (retries > 0) {
                try {
                    action();
                    break;
                }
                catch {
                    retries--;
                    if (retries == 0) {
                        throw;
                    }
                }
                Thread.Sleep(delayBeforeRetry);
            }
        }

        internal static void SmartCopy(string sourcePath, string destinationPath, DirectoryInfoBase sourceDirectory, DirectoryInfoBase destinationDirectory, bool skipOldFiles) {
            // Skip hidden directories and directories that begin with .
            if (sourceDirectory.Attributes.HasFlag(FileAttributes.Hidden) ||
                sourceDirectory.Name.StartsWith(".")) {
                return;
            }

            if (!destinationDirectory.Exists) {
                destinationDirectory.Create();
            }

            var destFilesLookup = GetFiles(destinationDirectory);
            var sourceFilesLookup = GetFiles(sourceDirectory);

            foreach (var destFile in destFilesLookup.Values) {
                // Check all files in the destination folder and remove those if they aren't 
                // in the source
                if (!sourceFilesLookup.ContainsKey(destFile.Name)) {
                    destFile.Delete();
                }
            }

            foreach (var sourceFile in sourceFilesLookup.Values) {
                // Skip files that start with .
                if (sourceFile.Name.StartsWith(".")) {
                    continue;
                }

                // If a file exists in the destination then only copy it again if it's
                // last write time is greater than the same file in the source (only if it changed)
                FileInfoBase targetFile;
                if (skipOldFiles &&
                    destFilesLookup.TryGetValue(sourceFile.Name, out targetFile) &&
                    sourceFile.LastWriteTimeUtc <= targetFile.LastWriteTimeUtc) {
                    continue;
                }

                // Otherwise, copy the file
                string path = GetDestinationPath(sourcePath, destinationPath, sourceFile);

                sourceFile.CopyTo(path, overwrite: true);
            }

            var sourceDirectoryLookup = GetDirectores(sourceDirectory);
            var destDirectoryLookup = GetDirectores(destinationDirectory);

            foreach (var destSubDirectory in destDirectoryLookup.Values) {
                if (!sourceDirectoryLookup.ContainsKey(destSubDirectory.Name)) {
                    // Delete all subdirectories that no longer exist in the source
                    destSubDirectory.Delete(recursive: true);
                }
            }

            foreach (var sourceSubDirectory in sourceDirectoryLookup.Values) {
                DirectoryInfoBase targetSubDirectory;
                if (!destDirectoryLookup.TryGetValue(sourceSubDirectory.Name, out targetSubDirectory)) {
                    string path = GetDestinationPath(sourcePath, destinationPath, sourceSubDirectory);
                    targetSubDirectory = new DirectoryInfoWrapper(new DirectoryInfo(path));
                }

                // Sync all sub directories
                SmartCopy(sourcePath, destinationPath, sourceSubDirectory, targetSubDirectory, skipOldFiles);
            }
        }

        private static string GetDestinationPath(string sourceRootPath, string destinationRootPath, FileSystemInfoBase info) {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        private static IDictionary<string, FileInfoBase> GetFiles(DirectoryInfoBase info) {
            return info.GetFiles().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static IDictionary<string, DirectoryInfoBase> GetDirectores(DirectoryInfoBase info) {
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}

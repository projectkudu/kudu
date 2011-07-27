using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kudu.Core.Deployment {
    internal static class DeploymentHelpers {
        internal static void SmartCopy(string sourcePath, string destinationPath, bool skipOldFiles = true) {
            SmartCopy(sourcePath, 
                      destinationPath, 
                      new DirectoryInfo(sourcePath), 
                      new DirectoryInfo(destinationPath),
                      skipOldFiles);
        }

        private static void SmartCopy(string sourcePath, string destinationPath, DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory, bool skipOldFiles) {
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
                FileInfo targetFile;
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
                DirectoryInfo targetSubDirectory;
                if (!destDirectoryLookup.TryGetValue(sourceSubDirectory.Name, out targetSubDirectory)) {
                    string path = GetDestinationPath(sourcePath, destinationPath, sourceSubDirectory);
                    targetSubDirectory = new DirectoryInfo(path);
                }

                // Sync all sub directories
                SmartCopy(sourcePath, destinationPath, sourceSubDirectory, targetSubDirectory, skipOldFiles);
            }
        }

        private static string GetDestinationPath(string sourceRootPath, string destinationRootPath, FileSystemInfo info) {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(sourceRootPath.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(destinationRootPath, sourcePath);
        }

        private static IDictionary<string, FileInfo> GetFiles(DirectoryInfo info) {
            return info.GetFiles().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        private static IDictionary<string, DirectoryInfo> GetDirectores(DirectoryInfo info) {
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}

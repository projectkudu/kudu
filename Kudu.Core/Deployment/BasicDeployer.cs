using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kudu.Core.Deployment {
    public class BasicDeployer : IDeployer {
        private readonly string _source;
        private readonly string _destination;

        public BasicDeployer(string source, string destination) {
            _source = source;
            _destination = destination;
        }

        public void Deploy(string id) {
            var source = new DirectoryInfo(_source);
            var dest = new DirectoryInfo(_destination);
            Sync(source, dest);
        }

        private void Sync(DirectoryInfo sourceDirectory, DirectoryInfo destinationDirectory) {
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
                if (destFilesLookup.TryGetValue(sourceFile.Name, out targetFile) &&
                    sourceFile.LastWriteTimeUtc <= targetFile.LastWriteTimeUtc) {
                    continue;
                }

                // Otherwise, copy the file
                string path = GetDestinationPath(sourceFile);
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
                    string path = GetDestinationPath(sourceSubDirectory);
                    targetSubDirectory = new DirectoryInfo(path);
                }

                // Sync all sub directories
                Sync(sourceSubDirectory, targetSubDirectory);
            }
        }

        private string GetDestinationPath(FileSystemInfo info) {
            string sourcePath = info.FullName;
            sourcePath = sourcePath.Substring(_source.Length)
                                   .Trim(Path.DirectorySeparatorChar);

            return Path.Combine(_destination, sourcePath);
        }

        private IDictionary<string, FileInfo> GetFiles(DirectoryInfo info) {
            return info.GetFiles().ToDictionary(f => f.Name, StringComparer.OrdinalIgnoreCase);
        }

        private IDictionary<string, DirectoryInfo> GetDirectores(DirectoryInfo info) {
            return info.GetDirectories().ToDictionary(d => d.Name, StringComparer.OrdinalIgnoreCase);
        }
    }
}

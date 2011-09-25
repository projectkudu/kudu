using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Commands {
    public class CommandExecutor : ICommandExecutor {
        private const string DriveLetters = "fghijklmnopqrstuvwxyz";

        private readonly string _workingDirectory;
        private readonly ConcurrentDictionary<string, char> _mappedPaths = new ConcurrentDictionary<string, char>(StringComparer.OrdinalIgnoreCase);
        private readonly IFileSystem _fileSystem;

        public CommandExecutor(IFileSystem fileSystem, string workingDirectory) {
            _fileSystem = fileSystem;
            _workingDirectory = workingDirectory;
        }

        public string ExecuteCommand(string command) {
            string path = GetMappedPath(_workingDirectory);
            var executable = new Executable("cmd", path);
            return executable.Execute("/c " + command);
        }

        public string GetMappedPath(string path) {
            var uri = new Uri(path);
            if (!uri.IsUnc) {
                // Not a UNC then do nothing
                return path;
            }

            // Skip \\ and split the path into segments
            var pathSegments = path.Substring(2).Split(Path.DirectorySeparatorChar);

            // Start with the first segment and try to find the shortest prefix that exists
            string prefix = String.Empty;
            for (int index = 0; index < pathSegments.Length; index++) {
                prefix = Path.Combine(prefix, pathSegments[index]);
                string subPath = @"\\" + prefix;

                // If \\foo\bar exists check if it's mapped already
                char mappedDrive;
                if (_mappedPaths.TryGetValue(subPath, out mappedDrive)) {
                    return GetMappedPath(pathSegments, index, mappedDrive);
                }

                // if it is then return mapped + baz\repository
                if (!_fileSystem.Directory.Exists(subPath)) {
                    continue;
                }

                // if it's not mapped then attempt to map it
                if (MapPath(subPath, out mappedDrive)) {
                    _mappedPaths.TryAdd(subPath, mappedDrive);
                    return GetMappedPath(pathSegments, index, mappedDrive);
                }
            }

            throw new InvalidOperationException(String.Format("Unable to map '{0}' to a drive.", path));
        }

        private static string GetMappedPath(IEnumerable<string> pathSegments, int index, char mappedDrive) {
            return Path.Combine(mappedDrive + @":\", pathSegments.Skip(index + 1).Aggregate(Path.Combine));
        }

        protected virtual bool MapPath(string path, out char driveLetter) {
            var cmd = new Executable("cmd", GetWindowsFolder());
            driveLetter = '\0';

            foreach (var letter in DriveLetters) {
                try {
                    cmd.Execute("/c net use {0}: {1}", letter, path);
                    driveLetter = letter;
                    return true;
                }
                catch {

                }
            }

            return false;
        }

        private static string GetWindowsFolder() {
            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);
        }
    }
}
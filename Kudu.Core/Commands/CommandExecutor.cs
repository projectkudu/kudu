using System;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Commands {
    public class CommandExecutor : ICommandExecutor {
        private readonly string _workingDirectory;

        public CommandExecutor(string workingDirectory) {
            _workingDirectory = workingDirectory;
        }

        public string ExecuteCommand(string command) {
            // REVIEW: Should we leave it mapped for performance reasons?
            using (var folder = new Folder(_workingDirectory)) {                
                var executable = new Executable("cmd", folder.Path);
                return executable.Execute("/c " + command);
            }
        }

        private class Folder : IDisposable {
            private const string DriveLetters = "fghijklmnopqrstuvwxyz";

            private readonly Executable _executable;
            private char? _driveLetter;

            public Folder(string path) {
                Path = path;

                // If the working directory is a share then we map that folder
                // to a drive temporarily so we don't need to change the registry
                // as described here (http://support.microsoft.com/kb/156276).
                // If this isn't a share then we don't attempt to do anything
                var uri = new Uri(path);
                if (uri.IsUnc) {
                    // Use windir as the working directory
                    _executable = new Executable("cmd", GetWindowsFolder());

                    // Get the share name
                    string share = path;

                    // Map the drive
                    MapShareToDrive(share);

                    // Replace \\sharename with the mapped drive
                    Path = Path.Replace(share, _driveLetter + @":\");
                }
            }

            public string Path {
                get;
                private set;
            }

            public void Dispose() {
                if (_driveLetter != null) {
                    _executable.Execute("/c net use {0}: /delete", _driveLetter);
                }
            }

            private void MapShareToDrive(string share) {
                foreach (var driveLetter in DriveLetters) {
                    try {
                        _executable.Execute("/c net use {0}: {1}", driveLetter, share);
                        _driveLetter = driveLetter;
                        return;
                    }
                    catch {

                    }
                }

                throw new InvalidOperationException(String.Format("Unable to map {0} to a drive", share));
            }

            private static string GetWindowsFolder() {
                return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);
            }
        }
    }
}
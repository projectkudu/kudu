using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Commands {
    public class CommandExecutor : ICommandExecutor {
        private const string DriveLetters = "fghijklmnopqrstuvwxyz";

        private readonly string _workingDirectory;
        private readonly IFileSystem _fileSystem;
        private IDictionary<string, string> _mappedDrives;
        private Process _executingProcess;

        public CommandExecutor(IFileSystem fileSystem, string workingDirectory) {
            _fileSystem = fileSystem;
            _workingDirectory = workingDirectory;
        }

        public event Action<CommandEvent> CommandEvent;

        public void ExecuteCommand(string command) {
            string path = GetMappedPath(_workingDirectory);

            _executingProcess = new Process();
            _executingProcess.StartInfo.FileName = "cmd";
            _executingProcess.StartInfo.WorkingDirectory = path;
            _executingProcess.StartInfo.Arguments = "/c " + command;
            _executingProcess.StartInfo.CreateNoWindow = true;
            _executingProcess.StartInfo.UseShellExecute = false;
            _executingProcess.StartInfo.RedirectStandardInput = true;
            _executingProcess.StartInfo.RedirectStandardOutput = true;
            _executingProcess.StartInfo.RedirectStandardError = true;
            _executingProcess.StartInfo.StandardOutputEncoding = Encoding.UTF8;
            _executingProcess.StartInfo.StandardErrorEncoding = Encoding.UTF8;
            _executingProcess.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
            _executingProcess.StartInfo.ErrorDialog = false;

            _executingProcess.Exited += (sender, e) => {
                if (CommandEvent != null) {
                    CommandEvent(new CommandEvent(CommandEventType.Complete));
                }
            };

            _executingProcess.OutputDataReceived += (sender, e) => {
                if (e.Data == null) {
                    return;
                }

                if (CommandEvent != null) {
                    CommandEvent(new CommandEvent(CommandEventType.Output, e.Data));
                }
            };

            _executingProcess.ErrorDataReceived += (sender, e) => {
                if (e.Data == null) {
                    return;
                }

                if (CommandEvent != null) {
                    CommandEvent(new CommandEvent(CommandEventType.Error, e.Data));
                }
            };

            _executingProcess.EnableRaisingEvents = true;
            _executingProcess.Start();
            _executingProcess.BeginErrorReadLine();
            _executingProcess.BeginOutputReadLine();

            _executingProcess.StandardInput.Close();
        }

        public void CancelCommand() {
            try {
                if (_executingProcess != null) {
                    _executingProcess.CancelErrorRead();
                    _executingProcess.CancelOutputRead();
                    _executingProcess.Kill();
                }
            }
            catch {
                // Swallow the exception, we don't care the if process can't be killed
            }
        }

        public string GetMappedPath(string path) {
            var uri = new Uri(path);
            if (!uri.IsUnc) {
                // Not a UNC then do nothing
                return path;
            }

            // We don't have to do this everytime, we can cache th results
            // for a little bit
            _mappedDrives = GetMappedNetworkDrives();

            // Skip \\ and split the path into segments
            var pathSegments = path.Substring(2).Split(Path.DirectorySeparatorChar);

            // Start with the first segment and try to find the shortest prefix that exists
            string prefix = String.Empty;
            for (int index = 0; index < pathSegments.Length; index++) {
                prefix = Path.Combine(prefix, pathSegments[index]);
                string subPath = @"\\" + prefix;

                // If \\foo\bar exists check if it's mapped already
                string driveName;
                if (TryGetMappedPath(subPath, out driveName)) {
                    return GetMappedPath(pathSegments, index, driveName);
                }

                // if it is then return mapped + baz\repository
                if (!_fileSystem.Directory.Exists(subPath)) {
                    continue;
                }

                // if it's not mapped then attempt to map it
                if (MapPath(subPath, out driveName)) {
                    return GetMappedPath(pathSegments, index, driveName);
                }
            }

            throw new InvalidOperationException(String.Format("Unable to map '{0}' to a drive.", path));
        }

        private static string GetMappedPath(IEnumerable<string> pathSegments, int index, string driveName) {
            return Path.Combine(driveName, pathSegments.Skip(index + 1).Aggregate(Path.Combine));
        }

        protected virtual bool MapPath(string path, out string driveName) {
            var cmd = new Executable("cmd", GetWindowsFolder());
            driveName = null;

            foreach (var letter in DriveLetters) {
                try {
                    // There's probably an API for this as well but this is easy to do
                    // Not as easy to parse out the results of net use
                    cmd.Execute("/c net use {0}: {1}", letter, path);
                    driveName = letter + @":\";
                    return true;
                }
                catch {

                }
            }

            return false;
        }

        protected virtual bool TryGetMappedPath(string path, out string driveName) {
            return _mappedDrives.TryGetValue(path, out driveName);
        }

        private static IDictionary<string, string> GetMappedNetworkDrives() {
            var mapping = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Loop over all mapped drives and get the see if there's already a unc path mapped
            foreach (var drive in DriveInfo.GetDrives()) {
                if (drive.DriveType != DriveType.Network || !drive.IsReady) {
                    continue;
                }

                var uncPathBuilder = new StringBuilder(255);
                int len = uncPathBuilder.Capacity;

                // Get the unc path
                NativeMethods.WNetGetConnection(drive.Name.Replace(@"\", ""), uncPathBuilder, ref len);

                string uncPath = uncPathBuilder.ToString();

                // Map the unc path to the drive name
                mapping[uncPath] = drive.Name;
            }

            return mapping;
        }

        private static string GetWindowsFolder() {
            return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);
        }
    }
}
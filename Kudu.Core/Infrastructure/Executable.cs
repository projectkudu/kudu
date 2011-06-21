using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Kudu.Core.Infrastructure {
    internal class Executable {
        private readonly string _path;
        private readonly string _workingDirectory;

        public Executable(string path, string workingDirectory) {
            _path = path;
            _workingDirectory = workingDirectory;
        }

        public string Execute(string arguments, params object[] args) {
            var psi = new ProcessStartInfo {
                FileName = _path,
                WorkingDirectory = _workingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                ErrorDialog = false,
                Arguments = String.Format(arguments, args)
            };

            var process = Process.Start(psi);

            Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();

            IAsyncResult outputReader = reader.BeginInvoke(process.StandardOutput, null, null);
            IAsyncResult errorReader = reader.BeginInvoke(process.StandardError, null, null);

            process.WaitForExit();

            string output = reader.EndInvoke(outputReader);
            string error = reader.EndInvoke(errorReader);

            // Sometimes, we get an exit code of 1 even when the command succeeds (e.g. with 'git reset .').
            // So also make sure there is an error string
            if (process.ExitCode != 0 && !String.IsNullOrEmpty(error)) {
                throw new Exception(error);
            }

            return output;
        }
    }
}

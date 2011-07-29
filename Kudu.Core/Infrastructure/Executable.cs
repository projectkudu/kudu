using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Kudu.Core.Infrastructure {
    internal class Executable {
        public Executable(string path, string workingDirectory) {
            Path = path;
            WorkingDirectory = workingDirectory;
        }

        public string WorkingDirectory { get; private set; }
        public string Path { get; private set; }

        public string Execute(string arguments, params object[] args) {
            var process = CreateProcess(arguments, args);

            Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();

            IAsyncResult outputReader = reader.BeginInvoke(process.StandardOutput, null, null);
            IAsyncResult errorReader = reader.BeginInvoke(process.StandardError, null, null);

            process.WaitForExit();

            string output = reader.EndInvoke(outputReader);
            string error = reader.EndInvoke(errorReader);

            // Sometimes, we get an exit code of 1 even when the command succeeds (e.g. with 'git reset .').
            // So also make sure there is an error string
            if (process.ExitCode != 0) {
                string text = String.IsNullOrEmpty(error) ? output : error;

                throw new Exception(text);
            }

            return output;
        }

        public void Execute(Stream input, Stream output, string arguments, params object[] args) {
            var process = CreateProcess(arguments, args);

            Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();
            Action<Stream, Stream> copyStream = (Stream from, Stream to) => from.CopyTo(to);

            IAsyncResult errorReader = reader.BeginInvoke(process.StandardError, null, null);
            IAsyncResult inputWriter = copyStream.BeginInvoke(input, process.StandardInput.BaseStream, null, null);
            IAsyncResult outputReader = copyStream.BeginInvoke(process.StandardOutput.BaseStream, output, null, null);

            process.WaitForExit();

            string error = reader.EndInvoke(errorReader);

            if (process.ExitCode != 0) {
                throw new Exception(error);
            }
        }

        private Process CreateProcess(string arguments, object[] args) {
            var psi = new ProcessStartInfo {
                FileName = Path,
                WorkingDirectory = WorkingDirectory,
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
            return process;
        }
    }
}

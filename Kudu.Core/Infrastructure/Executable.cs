using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    internal class Executable
    {
        public Executable(string path, string workingDirectory)
        {
            Path = path;
            WorkingDirectory = workingDirectory;
            EnvironmentVariables = new Dictionary<string, string>();
        }

        public bool IsAvailable
        {
            get
            {
                return File.Exists(Path);
            }
        }

        public string WorkingDirectory { get; private set; }
        public string Path { get; private set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; }

        public Tuple<string, string> Execute(string arguments, params object[] args)
        {
            return Execute(NullTracer.Instance, arguments, args);
        }

        public Tuple<string, string> Execute(ITracer tracer, string arguments, params object[] args)
        {
            using (GetProcessStep(tracer, arguments, args))
            {
                var process = CreateProcess(arguments, args);

                Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();

                IAsyncResult outputReader = reader.BeginInvoke(process.StandardOutput, null, null);
                IAsyncResult errorReader = reader.BeginInvoke(process.StandardError, null, null);

                process.StandardInput.Close();

                process.WaitForExit();

                string output = reader.EndInvoke(outputReader);
                string error = reader.EndInvoke(errorReader);

                // Sometimes, we get an exit code of 1 even when the command succeeds (e.g. with 'git reset .').
                // So also make sure there is an error string
                if (process.ExitCode != 0)
                {
                    string text = String.IsNullOrEmpty(error) ? output : error;

                    tracer.TraceError(text);

                    throw new Exception(text);
                }

                return Tuple.Create(output, error);
            }
        }

        public void Execute(ITracer tracer, Stream input, Stream output, string arguments, params object[] args)
        {
            using (GetProcessStep(tracer, arguments, args))
            {
                var process = CreateProcess(arguments, args);

                Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();
                Action<Stream, Stream, bool> copyStream = (Stream from, Stream to, bool closeAfterCopy) =>
                {
                    from.CopyTo(to);
                    if (closeAfterCopy)
                    {
                        to.Close();
                    }
                };

                IAsyncResult errorReader = reader.BeginInvoke(process.StandardError, null, null);
                if (input != null)
                {
                    // Copy into the input stream, and close it to tell the exe it can process it
                    copyStream.BeginInvoke(input, process.StandardInput.BaseStream, true, null, null);
                }

                // Copy the exe's output into the output stream
                copyStream.BeginInvoke(process.StandardOutput.BaseStream, output, false, null, null);

                process.WaitForExit();

                string error = reader.EndInvoke(errorReader);

                if (process.ExitCode != 0)
                {
                    throw new Exception(error);
                }

                Debug.WriteLine(error);
            }
        }

        private IDisposable GetProcessStep(ITracer tracer, string arguments, object[] args)
        {
            return tracer.Step("Executing external process", new Dictionary<string, string>
            {
                { "type", "process" },
                { "path", System.IO.Path.GetFileName(Path) },
                { "arguments", String.Format(arguments, args) }
            });
        }

        private Process CreateProcess(string arguments, object[] args)
        {
            var psi = new ProcessStartInfo
            {
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

            foreach (var pair in EnvironmentVariables)
            {
                psi.EnvironmentVariables[pair.Key] = pair.Value;
            }

            return Process.Start(psi);
        }
    }
}

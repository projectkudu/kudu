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

                tracer.Trace("Process dump", new Dictionary<string, string>
                {
                    { "outStream", output },
                    { "errorStream", error },
                    { "type", "processOutput" }
                });

                // Sometimes, we get an exit code of 1 even when the command succeeds (e.g. with 'git reset .').
                // So also make sure there is an error string
                if (process.ExitCode != 0)
                {
                    string text = String.IsNullOrEmpty(error) ? output : error;

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
                Action<Stream, Stream, bool, Func<IDisposable>> copyStream = (Stream from, Stream to, bool closeAfterCopy, Func<IDisposable> step) =>
                {
                    try
                    {
                        using (step())
                        {
                            from.CopyTo(to);
                            if (closeAfterCopy)
                            {
                                to.Close();

                                tracer.Trace("Stream closed after copy");
                            }
                            else
                            {
                                tracer.Trace("Stream left open after copy");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        tracer.TraceError(ex);

                        throw;
                    }
                };

                IAsyncResult errorReader = reader.BeginInvoke(process.StandardError, null, null);
                IAsyncResult inputResult = null;

                if (input != null)
                {
                    // Copy into the input stream, and close it to tell the exe it can process it
                    inputResult = copyStream.BeginInvoke(input,
                                                         process.StandardInput.BaseStream,
                                                         true,
                                                         () => tracer.Step("Copying input stream to stdin."),
                                                         null,
                                                         null);
                }

                // Copy the exe's output into the output stream
                IAsyncResult outputResult = copyStream.BeginInvoke(process.StandardOutput.BaseStream,
                                                                   output,
                                                                   false,
                                                                   () => tracer.Step("Copying stdout to output stream."),
                                                                   null,
                                                                   null);

                process.WaitForExit();

                // Wait for the input operation to complete
                if (inputResult != null)
                {
                    inputResult.AsyncWaitHandle.WaitOne();
                }

                // Wait for the output operation to be complete
                outputResult.AsyncWaitHandle.WaitOne();

                string error = reader.EndInvoke(errorReader);

                tracer.Trace("Process dump", new Dictionary<string, string>
                {
                    { "outStream", "" },
                    { "errorStream", error },
                    { "type", "processOutput" }
                });

                if (process.ExitCode != 0)
                {
                    throw new Exception(error);
                }
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

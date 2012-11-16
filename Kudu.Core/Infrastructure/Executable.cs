using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

#if !SITEMANAGEMENT
using Kudu.Contracts.Tracing;
using Kudu.Core.Tracing;
#endif

namespace Kudu.Core.Infrastructure
{
    internal class Executable
    {
        public Executable(string path, string workingDirectory)
        {
            Path = path;
            WorkingDirectory = workingDirectory;
            EnvironmentVariables = new Dictionary<string, string>();
            Encoding = Encoding.UTF8;
        }

        public bool IsAvailable
        {
            get
            {
                return File.Exists(Path);
            }
        }

        public void SetHomePath(string homePath)
        {
            // SSH requires HOME directory and applies to git, npm and (CustomBuilder) cmd
            // Excutable seems to be the most optimal class for this api.
            EnvironmentVariables["HOME"] = homePath;
            EnvironmentVariables["HOMEDRIVE"] = homePath.Substring(0, homePath.IndexOf(':') + 1);
            EnvironmentVariables["HOMEPATH"] = homePath.Substring(homePath.IndexOf(':') + 1);
        }

        public string WorkingDirectory { get; private set; }
        public string Path { get; private set; }
        public IDictionary<string, string> EnvironmentVariables { get; set; }
        public Encoding Encoding { get; set; }

#if !SITEMANAGEMENT
        public Tuple<string, string> Execute(string arguments, params object[] args)
        {
            return Execute(NullTracer.Instance, arguments, args);
        }

        public Tuple<string, string> Execute(ITracer tracer, string arguments, params object[] args)
#else
        public Tuple<string, string> Execute(string arguments, params object[] args)
#endif
        {

#if !SITEMANAGEMENT
            using (GetProcessStep(tracer, arguments, args))
            {
#endif
                var process = CreateProcess(arguments, args);
                process.Start();

                Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();

                IAsyncResult outputReader = reader.BeginInvoke(process.StandardOutput, null, null);
                IAsyncResult errorReader = reader.BeginInvoke(process.StandardError, null, null);

                process.StandardInput.Close();

                process.WaitForExit();

                string output = reader.EndInvoke(outputReader);
                string error = reader.EndInvoke(errorReader);

#if !SITEMANAGEMENT
                tracer.Trace("Process dump", new Dictionary<string, string>
                {
                    { "exitCode", process.ExitCode.ToString() },
                    { "type", "processOutput" }
                });
#endif

                // Sometimes, we get an exit code of 1 even when the command succeeds (e.g. with 'git reset .').
                // So also make sure there is an error string
                if (process.ExitCode != 0)
                {
                    string text = String.IsNullOrEmpty(error) ? output : error;

                    throw new CommandLineException(text)
                    {
                        ExitCode = process.ExitCode,
                        Output = output,
                        Error = error
                    };
                }

                return Tuple.Create(output, error);

#if !SITEMANAGEMENT
            }
#endif
        }

#if !SITEMANAGEMENT
        public void Execute(ITracer tracer, Stream input, Stream output, string arguments, params object[] args)
        {
            using (GetProcessStep(tracer, arguments, args))
            {
                var process = CreateProcess(arguments, args);
                process.Start();

                Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();
                Action<Stream, Stream, bool> copyStream = (Stream from, Stream to, bool closeAfterCopy) =>
                {
                    try
                    {
                        from.CopyTo(to);
                        if (closeAfterCopy)
                        {
                            to.Close();
                        }
                    }
                    catch (Exception ex)
                    {
                        tracer.TraceError(ex);
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
                                                         null,
                                                         null);
                }

                // Copy the exe's output into the output stream
                IAsyncResult outputResult = copyStream.BeginInvoke(process.StandardOutput.BaseStream,
                                                                   output,
                                                                   false,
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
                    { "exitCode", process.ExitCode.ToString() },
                    { "type", "processOutput" }
                });

                if (process.ExitCode != 0)
                {
                    throw new CommandLineException(error)
                    {
                        ExitCode = process.ExitCode,
                        Error = error
                    };
                }
            }
        }

        public Tuple<string, string> ExecuteWithConsoleOutput(ITracer tracer, string arguments, params object[] args)
        {
            return Execute(tracer,
                           output =>
                           {
                               Console.Out.WriteLine(output);
                               return true;
                           },
                           error =>
                           {
                               Console.Error.WriteLine(error);
                               return true;
                           },
                           Console.OutputEncoding,
                           arguments,
                           args);
        }

        public Tuple<string, string> Execute(ITracer tracer, Func<string, bool> onWriteOutput, Func<string, bool> onWriteError, Encoding encoding, string arguments, params object[] args)
        {
            using (GetProcessStep(tracer, arguments, args))
            {
                Process process = CreateProcess(arguments, args);
                process.EnableRaisingEvents = true;

                var errorBuffer = new StringBuilder();
                var outputBuffer = new StringBuilder();

                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        if (onWriteOutput(e.Data))
                        {
                            outputBuffer.AppendLine(Encoding.UTF8.GetString(encoding.GetBytes(e.Data)));
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        if (onWriteError(e.Data))
                        {
                            errorBuffer.AppendLine(Encoding.UTF8.GetString(encoding.GetBytes(e.Data)));
                        }
                    }
                };

                process.Start();

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                process.WaitForExit();

                tracer.Trace("Process dump", new Dictionary<string, string>
                {
                    { "exitCode", process.ExitCode.ToString() },
                    { "type", "processOutput" }
                });

                string output = outputBuffer.ToString().Trim();
                string error = errorBuffer.ToString().Trim();

                if (process.ExitCode != 0)
                {
                    string text = String.IsNullOrEmpty(error) ? output : error;

                    throw new CommandLineException(text)
                    {
                        ExitCode = process.ExitCode,
                        Output = output,
                        Error = error
                    };
                }

                return Tuple.Create(output, error);
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
#endif
        private Process CreateProcess(string arguments, object[] args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = Path,
                WorkingDirectory = WorkingDirectory,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                UseShellExecute = false,
                ErrorDialog = false,
                Arguments = String.Format(arguments, args)
            };

            if (Encoding != null)
            {
                psi.StandardOutputEncoding = Encoding;
                psi.StandardErrorEncoding = Encoding;
            }

            foreach (var pair in EnvironmentVariables)
            {
                psi.EnvironmentVariables[pair.Key] = pair.Value;
            }

            var process = new Process()
            {
                StartInfo = psi
            };

            return process;
        }
    }
}

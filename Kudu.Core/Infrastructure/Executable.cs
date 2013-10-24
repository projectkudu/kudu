using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Tracing;

namespace Kudu.Core.Infrastructure
{
    public class Executable : Kudu.Core.Infrastructure.IExecutable
    {
        public Executable(string path, string workingDirectory, TimeSpan idleTimeout)
        {
            Path = path;
            WorkingDirectory = workingDirectory;
            EnvironmentVariables = new Dictionary<string, string>();
            Encoding = Encoding.UTF8;
            IdleTimeout = idleTimeout;
        }

        public bool IsAvailable
        {
            get
            {
                return File.Exists(Path);
            }
        }

        public void SetHomePath(IEnvironment environment)
        {
            if (!String.IsNullOrEmpty(environment.RootPath))
            {
                SetHomePath(environment.RootPath);
            }
        }

        public void SetHomePath(string homePath)
        {
            // SSH requires HOME directory and applies to git, npm and (CustomBuilder) cmd
            // Don't set it if it's already set, as would be the case in Azure
            if (String.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("HOME")))
            {
                EnvironmentVariables["HOME"] = homePath;
            }

            EnvironmentVariables["HOMEDRIVE"] = homePath.Substring(0, homePath.IndexOf(':') + 1);
            EnvironmentVariables["HOMEPATH"] = homePath.Substring(homePath.IndexOf(':') + 1);
        }

        public string WorkingDirectory { get; private set; }

        public string Path { get; private set; }

        public IDictionary<string, string> EnvironmentVariables { get; set; }

        public Encoding Encoding { get; set; }

        public TimeSpan IdleTimeout { get; private set; }

        public Tuple<string, string> Execute(string arguments, params object[] args)
        {
            return Execute(NullTracer.Instance, arguments, args);
        }

        public Tuple<string, string> Execute(ITracer tracer, string arguments, params object[] args)
        {
            using (GetProcessStep(tracer, arguments, args))
            {
                var process = CreateProcess(arguments, args);
                process.Start();

                var idleManager = new Kudu.Core.Infrastructure.IdleManager(IdleTimeout, tracer);
                Func<StreamReader, Task<string>> reader = async (StreamReader streamReader) =>
                {
                    var strb = new StringBuilder();
                    char[] buffer = new char[1024];
                    int read;
                    while ((read = await streamReader.ReadBlockAsync(buffer, 0, buffer.Length)) != 0)
                    {
                        idleManager.UpdateActivity();
                        strb.Append(buffer, 0, read);
                    }
                    idleManager.UpdateActivity();
                    return strb.ToString();
                };

                Task<string> outputReaderTask = Task.Run(async () => await reader(process.StandardOutput));
                Task<string> errorReaderTask = Task.Run(async () => await reader(process.StandardError));

                process.StandardInput.Close();

                idleManager.WaitForExit(process);

                string output = outputReaderTask.Result;
                string error = errorReaderTask.Result;

                tracer.TraceProcessExitCode(process);

                // Sometimes, we get an exit code of 1 even when the command succeeds (e.g. with 'git reset .').
                // So also make sure there is an error string
                if (process.ExitCode != 0)
                {
                    string text = String.IsNullOrEmpty(error) ? output : error;

                    throw new CommandLineException(Path, process.StartInfo.Arguments, text)
                    {
                        ExitCode = process.ExitCode,
                        Output = output,
                        Error = error
                    };
                }

                return Tuple.Create(output, error);
            }
        }

        public void Execute(ITracer tracer, Stream input, Stream output, string arguments, params object[] args)
        {
            using (GetProcessStep(tracer, arguments, args))
            {
                var process = CreateProcess(arguments, args);
                process.Start();

                var idleManager = new IdleManager(IdleTimeout, tracer, output);
                Func<StreamReader, string> reader = (StreamReader streamReader) => streamReader.ReadToEnd();
                Action<Stream, Stream, bool> copyStream = (Stream from, Stream to, bool closeAfterCopy) =>
                {
                    try
                    {
                        byte[] bytes = new byte[1024];
                        int read = 0;
                        bool writeError = false;
                        while ((read = from.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            idleManager.UpdateActivity();
                            try
                            {
                                if (!writeError)
                                {
                                    to.Write(bytes, 0, read);
                                }
                            }
                            catch (Exception ex)
                            {
                                writeError = true;
                                tracer.TraceError(ex);
                            }
                        }

                        idleManager.UpdateActivity();
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

                idleManager.WaitForExit(process);

                // Wait for the input operation to complete
                if (inputResult != null)
                {
                    inputResult.AsyncWaitHandle.WaitOne();
                }

                // Wait for the output operation to be complete
                outputResult.AsyncWaitHandle.WaitOne();

                string error = reader.EndInvoke(errorReader);

                tracer.TraceProcessExitCode(process);

                if (process.ExitCode != 0)
                {
                    throw new CommandLineException(Path, process.StartInfo.Arguments, error)
                    {
                        ExitCode = process.ExitCode,
                        Error = error
                    };
                }
            }
        }

        public Tuple<string, string> ExecuteWithProgressWriter(ILogger logger, ITracer tracer, string arguments, params object[] args)
        {
            try
            {
                using (var writer = new ProgressWriter())
                {
                    return Execute(tracer,
                                   output =>
                                   {
                                       writer.WriteOutLine(output);
                                       logger.Log(output);
                                       return true;
                                   },
                                   error =>
                                   {
                                       writer.WriteErrorLine(error);
                                       logger.Log(error, LogEntryType.Warning);
                                       return true;
                                   },
                                   Console.OutputEncoding,
                                   arguments,
                                   args);
                }
            }
            catch (CommandLineException exception)
            {
                // in case of failure without stderr, we log error explicitly
                if (String.IsNullOrEmpty(exception.Error))
                {
                    logger.Log(exception);
                }

                throw;
            }
            catch (Exception exception)
            {
                // in case of other failure, we log error explicitly
                logger.Log(exception);

                throw;
            }
        }

        public int ExecuteReturnExitCode(ITracer tracer, Func<string, bool> onWriteOutput, Func<string, bool> onWriteError, string arguments, params object[] args)
        {
            try
            {
                ExecuteInternal(tracer, onWriteOutput, onWriteError, null, arguments, args);
            }
            catch (CommandLineException ex)
            {
                return ex.ExitCode;
            }

            return 0;
        }

        public Tuple<string, string> Execute(ITracer tracer, Func<string, bool> onWriteOutput, Func<string, bool> onWriteError, Encoding encoding, string arguments, params object[] args)
        {
            return ExecuteInternal(tracer, onWriteOutput, onWriteError, encoding, arguments, args);
        }

        private Tuple<string, string> ExecuteInternal(ITracer tracer, Func<string, bool> onWriteOutput, Func<string, bool> onWriteError, Encoding encoding, string arguments, params object[] args)
        {
            using (GetProcessStep(tracer, arguments, args))
            {
                Process process = CreateProcess(arguments, args);
                process.EnableRaisingEvents = true;

                var errorBuffer = new StringBuilder();
                var outputBuffer = new StringBuilder();

                var idleManager = new IdleManager(IdleTimeout, tracer);
                process.OutputDataReceived += (sender, e) =>
                {
                    idleManager.UpdateActivity();
                    if (e.Data != null)
                    {
                        if (onWriteOutput(e.Data) && encoding != null)
                        {
                            outputBuffer.AppendLine(Encoding.UTF8.GetString(encoding.GetBytes(e.Data)));
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    idleManager.UpdateActivity();
                    if (e.Data != null)
                    {
                        if (onWriteError(e.Data) && encoding != null)
                        {
                            errorBuffer.AppendLine(Encoding.UTF8.GetString(encoding.GetBytes(e.Data)));
                        }
                    }
                };

                process.Start();

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                try
                {
                    idleManager.WaitForExit(process);
                }
                catch (Exception ex)
                {
                    onWriteError(ex.Message);
                    throw;
                }

                tracer.TraceProcessExitCode(process);

                string output = outputBuffer.ToString().Trim();
                string error = errorBuffer.ToString().Trim();

                if (process.ExitCode != 0)
                {
                    string text = String.IsNullOrEmpty(error) ? output : error;

                    throw new CommandLineException(Path, process.StartInfo.Arguments, text)
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

        internal Process CreateProcess(string arguments, object[] args)
        {
            return CreateProcess(String.Format(arguments, args));
        }

        internal Process CreateProcess(string arguments)
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
                Arguments = arguments
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

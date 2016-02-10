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
            EnvironmentVariables["HOMEDRIVE"] = homePath.Substring(0, homePath.IndexOf(':') + 1);
            EnvironmentVariables["HOMEPATH"] = homePath.Substring(homePath.IndexOf(':') + 1);
        }

        public string WorkingDirectory { get; private set; }

        public string Path { get; private set; }

        public IDictionary<string, string> EnvironmentVariables { get; set; }

        public Encoding Encoding { get; set; }

        public TimeSpan IdleTimeout { get; private set; }

        // this is for simple execution just to get stdout such as 'git init .'
        public Tuple<string, string> Execute(string arguments, params object[] args)
        {
            return Execute(NullTracer.Instance, arguments, args);
        }

        // this is for simple execution just to get stdout such as 'git init .'
        public Tuple<string, string> Execute(ITracer tracer, string arguments, params object[] args)
        {
            var cmdArguments = String.Format(arguments, args);
            var outputStream = new MemoryStream();
            var errorStream = new MemoryStream();

            // common execute
            int exitCode = Task.Run(() => ExecuteAsync(tracer, cmdArguments, outputStream, errorStream)).Result;

            string output = GetString(outputStream);
            string error = GetString(errorStream);
            if (exitCode != 0)
            {
                throw new CommandLineException(Path, cmdArguments, !String.IsNullOrEmpty(error) ? error : output)
                {
                    ExitCode = exitCode,
                    Output = output,
                    Error = error
                };
            }

            return Tuple.Create(output, error);
        }

        // this is used exclusive in git sever scenario
        public void Execute(ITracer tracer, Stream input, Stream output, string arguments, params object[] args)
        {
            var cmdArguments = String.Format(arguments, args);
            var errorStream = new MemoryStream();
            var idleManager = new IdleManager(IdleTimeout, tracer, output);

            // common execute
            int exitCode = Task.Run(() => ExecuteAsync(tracer, cmdArguments, output, errorStream, input, idleManager)).Result;

            string error = GetString(errorStream);
            if (exitCode != 0)
            {
                throw new CommandLineException(Path, cmdArguments, error)
                {
                    ExitCode = exitCode,
                    Error = error
                };
            }
        }

        // this is used for long running command that requires ongoing progress such as job, build script etc.
        public Tuple<string, string> ExecuteWithProgressWriter(ILogger logger, ITracer tracer, string arguments, params object[] args)
        {
            try
            {
                using (var writer = new ProgressWriter())
                {
                    return ExecuteInternal(tracer,
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
                logger.Log(exception);

                throw;
            }
            catch (Exception exception)
            {
                // in case of other failure, we log error explicitly
                logger.Log(exception);

                throw;
            }
        }

        // this is used for long running command that requires ongoing progress such as job, build script etc.
        public int ExecuteReturnExitCode(ITracer tracer, Action<string> onWriteOutput, Action<string> onWriteError, string arguments, params object[] args)
        {
            try
            {
                Func<string, bool> writeOutput = (message) =>
                {
                    onWriteOutput(message);
                    return false;
                };

                Func<string, bool> writeError = (message) =>
                {
                    onWriteError(message);
                    return false;
                };

                ExecuteInternal(tracer, writeOutput, writeError, null, arguments, args);
            }
            catch (CommandLineException ex)
            {
                return ex.ExitCode;
            }

            return 0;
        }

        private Tuple<string, string> ExecuteInternal(ITracer tracer, Func<string, bool> onWriteOutput, Func<string, bool> onWriteError, Encoding encoding, string arguments, params object[] args)
        {
            var cmdArguments = String.Format(arguments, args);
            var errorBuffer = new StringBuilder();
            var outputBuffer = new StringBuilder();
            var idleManager = new IdleManager(IdleTimeout, tracer);
            var outputStream = new AsyncStreamWriter(data =>
            {
                idleManager.UpdateActivity();
                if (data != null)
                {
                    if (onWriteOutput(data))
                    {
                        outputBuffer.AppendLine(Encoding.UTF8.GetString(encoding.GetBytes(data)));
                    }
                }
            }, Encoding ?? Console.OutputEncoding);
            var errorStream = new AsyncStreamWriter(data =>
            {
                idleManager.UpdateActivity();
                if (data != null)
                {
                    if (onWriteError(data))
                    {
                        errorBuffer.AppendLine(Encoding.UTF8.GetString(encoding.GetBytes(data)));
                    }
                }
            }, Encoding ?? Console.OutputEncoding);

            int exitCode;
            try
            {
                // common execute
                exitCode = Task.Run(() => ExecuteAsync(tracer, cmdArguments, outputStream, errorStream, idleManager: idleManager)).Result;
            }
            catch (AggregateException ex)
            {
                foreach (var inner in ex.Flatten().InnerExceptions)
                {
                    onWriteError(inner.Message);
                }
                throw;
            }
            catch (Exception ex)
            {
                onWriteError(ex.Message);
                throw;
            }
            finally
            {
                // flush out last buffer if any
                outputStream.Dispose();
                errorStream.Dispose();
            }

            string output = outputBuffer.ToString().Trim();
            string error = errorBuffer.ToString().Trim();

            if (exitCode != 0)
            {
                throw new CommandLineException(Path, cmdArguments, error)
                {
                    ExitCode = exitCode,
                    Output = output,
                    Error = error
                };
            }

            return Tuple.Create(output, error);
        }

        // This is pure async process execution
        public async Task<int> ExecuteAsync(ITracer tracer, string arguments, Stream output, Stream error, Stream input = null, IdleManager idleManager = null)
        {
            using (GetProcessStep(tracer, arguments))
            {
                using (Process process = CreateProcess(arguments))
                {
                    var wrapper = new ProcessWrapper(process);

                    int exitCode = await wrapper.Start(tracer, output, error, input, idleManager ?? new IdleManager(IdleTimeout, tracer));

                    tracer.TraceProcessExitCode(process);

                    return exitCode;
                }
            }
        }

        private string GetString(MemoryStream stream)
        {
            if (stream.Length > 0)
            {
                var encoding = Encoding ?? Console.OutputEncoding;
                return encoding.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }

            return String.Empty;
        }

        private IDisposable GetProcessStep(ITracer tracer, string arguments)
        {
            return tracer.Step(XmlTracer.ExecutingExternalProcessTrace, new Dictionary<string, string>
            {
                { "type", "process" },
                { "path", System.IO.Path.GetFileName(Path) },
                { "arguments", arguments }
            });
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
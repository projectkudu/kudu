using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text;
using System.Threading;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Commands
{
    public class CommandExecutor : ICommandExecutor
    {
        private readonly string _workingDirectory;
        private Process _executingProcess;

        public CommandExecutor(string workingDirectory)
        {
            _workingDirectory = workingDirectory;
        }

        public event Action<CommandEvent> CommandEvent;

        public CommandResult ExecuteCommand(string command)
        {
            var result = new CommandResult();

            int exitCode = 0;
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            Action<CommandEvent> handler = args =>
            {
                switch (args.EventType)
                {
                    case CommandEventType.Output:
                        outputBuilder.AppendLine(args.Data);
                        break;
                    case CommandEventType.Error:
                        errorBuilder.AppendLine(args.Data);
                        break;
                    case CommandEventType.Complete:
                        exitCode = args.ExitCode;
                        break;
                    default:
                        break;
                }
            };

            try
            {
                // Code reuse is good
                CommandEvent += handler;

                ExecuteCommandAsync(command);
            }
            finally
            {
                CommandEvent -= handler;
            }

            _executingProcess.WaitForExit();

            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
            result.ExitCode = exitCode;

            return result;
        }

        public void ExecuteCommandAsync(string command)
        {
            string path = _workingDirectory;

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
            var pathEnv = _executingProcess.StartInfo.EnvironmentVariables["PATH"];
            if (!pathEnv.EndsWith(";"))
            {
                pathEnv += ";";
            }
            pathEnv += PathUtility.ResolveGitBinPath();
            _executingProcess.StartInfo.EnvironmentVariables["PATH"] = pathEnv;

            var commandEvent = CommandEvent;

            _executingProcess.Exited += (sender, e) =>
            {
                if (commandEvent != null)
                {
                    commandEvent(new CommandEvent(CommandEventType.Complete)
                    {
                        ExitCode = _executingProcess.ExitCode
                    });
                }
            };

            _executingProcess.OutputDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                if (commandEvent != null)
                {
                    commandEvent(new CommandEvent(CommandEventType.Output, e.Data));
                }
            };

            _executingProcess.ErrorDataReceived += (sender, e) =>
            {
                if (e.Data == null)
                {
                    return;
                }

                if (commandEvent != null)
                {
                    commandEvent(new CommandEvent(CommandEventType.Error, e.Data));
                }
            };

            _executingProcess.EnableRaisingEvents = true;
            _executingProcess.Start();
            _executingProcess.BeginErrorReadLine();
            _executingProcess.BeginOutputReadLine();

            _executingProcess.StandardInput.Close();
        }

        public void CancelCommand()
        {
            try
            {
                if (_executingProcess != null)
                {
                    _executingProcess.CancelErrorRead();
                    _executingProcess.CancelOutputRead();
                    _executingProcess.Kill();
                }
            }
            catch
            {
                // Swallow the exception, we don't care the if process can't be killed
            }
        }
    }
}
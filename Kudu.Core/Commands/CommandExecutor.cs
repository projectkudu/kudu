using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Commands
{
    public class CommandExecutor : ICommandExecutor
    {
        private Process _executingProcess;
        private IEnvironment _environment;
        private string _rootDirectory;
        private ExternalCommandFactory _externalCommandFactory;
        private readonly IDeploymentSettingsManager _settings;
        private readonly ITracer _tracer;

        public CommandExecutor(IEnvironment environment, IDeploymentSettingsManager settings, ITracer tracer)
        {
            var repositoryPath = environment.RootPath;
            _rootDirectory = repositoryPath;
            _environment = environment;
            _externalCommandFactory = new ExternalCommandFactory(environment, settings, repositoryPath);
            _settings = settings;
            _tracer = tracer;
        }

        public event Action<CommandEvent> CommandEvent;

        public CommandResult ExecuteCommand(string command, string workingDirectory)
        {
            var idleManager = new IdleManager(_settings.GetCommandIdleTimeout(), _tracer);
            var result = new CommandResult();

            int exitCode = 0;
            var outputBuilder = new StringBuilder();
            var errorBuilder = new StringBuilder();

            Action<CommandEvent> handler = args =>
            {
                idleManager.UpdateActivity();
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

                ExecuteCommandAsync(command, workingDirectory);
            }
            finally
            {
                CommandEvent -= handler;
            }

            idleManager.WaitForExit(_executingProcess);

            result.Output = outputBuilder.ToString();
            result.Error = errorBuilder.ToString();
            result.ExitCode = exitCode;

            return result;
        }

        public void ExecuteCommandAsync(string command, string relativeWorkingDirectory)
        {
            string workingDirectory;
            if (String.IsNullOrEmpty(relativeWorkingDirectory))
            {
                workingDirectory = _rootDirectory;
            }
            else
            {
                workingDirectory = Path.Combine(_rootDirectory, System.Environment.ExpandEnvironmentVariables(relativeWorkingDirectory));
            }

            Executable exe = _externalCommandFactory.BuildExternalCommandExecutable(workingDirectory, _environment.WebRootPath, NullLogger.Instance);
            _executingProcess = exe.CreateProcess(command);

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
                    _executingProcess.Kill(includesChildren: true, tracer: _tracer);
                }
            }
            catch
            {
                // Swallow the exception, we don't care the if process can't be killed
            }
        }
    }
}
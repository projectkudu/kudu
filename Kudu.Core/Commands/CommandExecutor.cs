using System;
using System.IO;
using System.Linq;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Commands {
    public class CommandExecutor : ICommandExecutor {
        private readonly string _workingDirectory;

        public CommandExecutor(string workingDirectory) {
            _workingDirectory = workingDirectory;
        }

        public string ExecuteCommand(string command) {            
            // TODO: Use an actual command parser
            var segments = command.Split(' ');
            string exePath = GetExecutablePath(segments[0]);
            var executable = new Executable(exePath, _workingDirectory);
            return executable.Execute(String.Join(" ", segments.Skip(1)));
        }

        private string GetExecutablePath(string commandName) {
            // TODO: Inject this logic via some kind of ICommandResolver
            var paths = System.Environment.GetEnvironmentVariable("PATH").Split(';');
            var suffixes = new[] { "", ".exe", ".bat", ".cmd" };
            
            var match = (from path in paths
                         from suffix in suffixes
                         select Path.Combine(path, commandName + suffix) into candidate
                         where File.Exists(candidate)
                         select candidate).FirstOrDefault();

            if (match != null) {
                return match;
            }

            throw new InvalidOperationException(String.Format("Unknown command '{0}'", commandName));
        }
    }
}

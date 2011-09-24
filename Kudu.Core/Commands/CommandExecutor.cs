using Kudu.Core.Infrastructure;

namespace Kudu.Core.Commands {
    public class CommandExecutor : ICommandExecutor {
        private readonly string _workingDirectory;

        public CommandExecutor(string workingDirectory) {
            _workingDirectory = workingDirectory;
        }

        public string ExecuteCommand(string command) {            
            var executable = new Executable("cmd", _workingDirectory);
            return executable.Execute("/c " + command);
        }        
    }
}

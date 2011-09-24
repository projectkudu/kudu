using System;

namespace Kudu.Core.Commands {
    public class CommandExecutor : ICommandExecutor {
        public string ExecuteCommand(string command) {
            return "This is some sample output\nThis is another line";
        }
    }
}

using System;
using System.Collections.Generic;
using Kudu.Core.Commands;
using SignalR.Hubs;

namespace Kudu.Client.Hubs.Editor {
    public class CommandLine : Hub {
        private readonly ICommandExecutor _executor;

        public CommandLine(ICommandExecutor executor) {
            _executor = executor;
        }

        public IEnumerable<string> Run(string command) {
            string output = _executor.ExecuteCommand(command);
            return output.Replace(Environment.NewLine, "\n").Split('\n');
        }
    }
}

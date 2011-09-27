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

        public string Run(string command) {
            string output = _executor.ExecuteCommand(command);
            if (output != null) {
                return output.TrimEnd();
            }
            return output;
        }
    }
}

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
            return _executor.ExecuteCommand(command);
        }
    }
}

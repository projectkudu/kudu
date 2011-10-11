using System;
using System.Collections.Generic;
using Kudu.Core.Commands;
using SignalR.Hubs;
using Kudu.Client.Infrastructure;

namespace Kudu.Client.Hubs.Editor {
    public class CommandLine : Hub {
        private readonly ISiteConfiguration _siteConfiguration;

        public CommandLine(ISiteConfiguration siteConfiguration) {
            _siteConfiguration = siteConfiguration;
        }

        public void Run(string command) {
            GetActiveExecutor().ExecuteCommand(command);
        }

        public void Cancel() {
            GetActiveExecutor().CancelCommand();
        }

        private ICommandExecutor GetActiveExecutor() {
            string mode = Caller.mode;
            if (String.IsNullOrEmpty(mode)) {
                return _siteConfiguration.CommandExecutor;
            }
            return _siteConfiguration.DevCommandExecutor;
        }
    }
}

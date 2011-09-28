using System;

namespace Kudu.Core.Commands {
    public interface ICommandExecutor {
        void ExecuteCommand(string command);

        event Action<CommandEvent> CommandEvent;
    }
}

using System;

namespace Kudu.Core.Commands {
    public interface ICommandExecutor {
        void ExecuteCommand(string command);
        void CancelCommand();

        event Action<CommandEvent> CommandEvent;
    }
}

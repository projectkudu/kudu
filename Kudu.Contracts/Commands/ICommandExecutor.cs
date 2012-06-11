using System;

namespace Kudu.Core.Commands
{
    public interface ICommandExecutor
    {
        CommandResult ExecuteCommand(string command);

        void ExecuteCommandAsync(string command);
        void CancelCommand();

        event Action<CommandEvent> CommandEvent;
    }
}

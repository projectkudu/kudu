namespace Kudu.Core.Commands {
    public interface ICommandExecutor {
        string ExecuteCommand(string command);
    }
}

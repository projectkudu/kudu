namespace Kudu.Core.Infrastructure
{
    public interface IServerConfiguration
    {
        string ApplicationName { get; }

        string GitServerRoot { get; }
    }
}

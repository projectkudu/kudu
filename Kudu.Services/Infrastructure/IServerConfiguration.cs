namespace Kudu.Services
{
    public interface IServerConfiguration
    {
        string ApplicationName { get; }
        string GitServerRoot { get; }
    }
}

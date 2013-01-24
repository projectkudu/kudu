namespace Kudu.Core.Deployment
{
    public interface ILogger
    {
        ILogger Log(string value, LogEntryType type);
    }
}

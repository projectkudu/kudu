namespace Kudu.Core.Deployment {
    public interface ILogger {
        void Log(string value, LogEntryType type);
    }
}

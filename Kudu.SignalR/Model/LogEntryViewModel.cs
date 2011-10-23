using Kudu.Client.Deployment;
using Kudu.Core.Deployment;

namespace Kudu.SignalR.Model {
    public class LogEntryViewModel {
        public LogEntryViewModel(LogEntry entry) {
            LogTime = entry.LogTime.ToString();
            Message = entry.Message;
            Type = entry.Type;
        }

        public LogEntryType Type { get; set; }
        public string LogTime { get; private set; }
        public string Message { get; private set; }
    }
}
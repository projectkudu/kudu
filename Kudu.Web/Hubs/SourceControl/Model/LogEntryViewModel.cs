using Kudu.Core.Deployment;

namespace Kudu.Web.Model {
    public class LogEntryViewModel {
        public LogEntryViewModel(LogEntry entry) {
            LogTime = entry.LogTime.ToString();
            Message = entry.Message;
        }

        public string LogTime { get; private set; }
        public string Message { get; private set; }
    }
}
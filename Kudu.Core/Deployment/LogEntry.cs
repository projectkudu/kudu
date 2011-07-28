using System;

namespace Kudu.Core.Deployment {
    public class LogEntry {
        public DateTime LogTime { get; private set; }
        public string Message { get; private set; }


        public LogEntry(DateTime logTime, string message) {
            LogTime = logTime;
            Message = message;
        }
    }
}

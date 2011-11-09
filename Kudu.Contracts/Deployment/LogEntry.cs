using System;

namespace Kudu.Core.Deployment
{
    public class LogEntry
    {
        public DateTime LogTime { get; private set; }
        public string Message { get; private set; }
        public LogEntryType Type { get; set; }

        public LogEntry(DateTime logTime, string message, LogEntryType type)
        {
            LogTime = logTime;
            Message = message;
            Type = type;
        }
    }
}

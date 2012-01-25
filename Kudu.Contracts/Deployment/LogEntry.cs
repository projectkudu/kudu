using System;

namespace Kudu.Core.Deployment
{
    public class LogEntry
    {
        public DateTime LogTime { get; set; }
        public string EntryId { get; set; }
        public string Message { get; set; }
        public LogEntryType Type { get; set; }

        public LogEntry()
        {
        }

        public LogEntry(DateTime logTime, string entryId, string message, LogEntryType type)
        {
            LogTime = logTime;
            EntryId = entryId;
            Message = message;
            Type = type;
        }
    }
}

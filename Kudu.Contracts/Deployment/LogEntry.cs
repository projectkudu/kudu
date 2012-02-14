using System;
using System.Runtime.Serialization;

namespace Kudu.Core.Deployment
{
    [DataContract]
    public class LogEntry
    {
        [DataMember(Name = "log_time")]
        public DateTime LogTime { get; set; }

        [DataMember(Name = "id")]
        public string EntryId { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "type")]
        public LogEntryType Type { get; set; }

        [DataMember(Name = "details_url")]
        public Uri DetailsUrl { get; set; }

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

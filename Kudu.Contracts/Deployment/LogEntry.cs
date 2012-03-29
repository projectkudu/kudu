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
        public string Id { get; set; }

        [DataMember(Name = "message")]
        public string Message { get; set; }

        [DataMember(Name = "type")]
        public LogEntryType Type { get; set; }

        [DataMember(Name = "details_url")]
        public Uri DetailsUrl { get; set; }

        public bool HasDetails { get; set; }

        public LogEntry()
        {
        }

        public LogEntry(DateTime logTime, string id, string message, LogEntryType type)
        {
            LogTime = logTime;
            Id = id;
            Message = message;
            Type = type;
        }
    }
}

using System;
using Newtonsoft.Json;

namespace Kudu.Core.Deployment
{
    public class LogEntry
    {
        [JsonProperty(PropertyName = "log_time")]
        public DateTime LogTime { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        [JsonProperty(PropertyName = "type")]
        public LogEntryType Type { get; set; }

        [JsonProperty(PropertyName = "details_url")]
        public Uri DetailsUrl { get; set; }

        [JsonIgnore]
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

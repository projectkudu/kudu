using System;
using System.Diagnostics.CodeAnalysis;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Core.Deployment
{
    public class LogEntry : INamedObject
    {
        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM spceific name")]
        string INamedObject.Name { get { return Id; } }

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

        public override string ToString()
        {
            return Message;
        }
    }
}

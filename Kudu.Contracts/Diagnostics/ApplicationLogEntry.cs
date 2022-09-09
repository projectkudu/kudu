using System;
using System.Globalization;
using Newtonsoft.Json;

namespace Kudu.Contracts.Diagnostics
{
    public class ApplicationLogEntry
    {
        [JsonProperty(PropertyName = "timestamp")]
        public DateTimeOffset TimeStamp { get; set; }

        [JsonProperty(PropertyName = "level")]
        public string Level { get; set; }

        [JsonProperty(PropertyName = "pid")]
        public int PID { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        public ApplicationLogEntry()
        {
        }

        public ApplicationLogEntry(DateTime eventDate, string level, int pid, string message)
        {
            TimeStamp = eventDate;
            Level = level;
            PID = pid;
            Message = message;
        }

        public void AddMessageLine(string messagePart)
        {
            this.Message = string.IsNullOrEmpty(this.Message)
                ? messagePart
                : messagePart + System.Environment.NewLine + this.Message;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "Application Log Entry, TimeStamp: {0}, Level: {1}, PID: {2}, Message: {3}", TimeStamp, Level, PID, Message);
        }
    }
}

using System;
using System.Globalization;
using System.Text.Json.Serialization;

namespace Kudu.Contracts.Diagnostics
{
    public class ApplicationLogEntry
    {
        [JsonPropertyName("timestamp")]
        public DateTimeOffset TimeStamp { get; set; }

        [JsonPropertyName("level")]
        public string Level { get; set; }

        [JsonPropertyName("pid")]
        public int PID { get; set; }

        [JsonPropertyName("message")]
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

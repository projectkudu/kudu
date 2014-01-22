using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Contracts.Diagnostics
{
    [DataContract]
    public class ApplicationLogEntry
    {
        [DataMember(Name = "timestamp")]
        public DateTimeOffset TimeStamp { get; set; }

        [DataMember(Name = "level")]
        public string Level { get; set; }

        [DataMember(Name = "pid")]
        public int PID { get; set; }

        [DataMember(Name = "message")]
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

using System;
using System.Runtime.Serialization;

namespace Kudu.Contracts.Jobs
{
    [DataContract]
    public class TriggeredJobRun
    {
        [DataMember(Name = "id")]
        public string Id { get; set; }

        [DataMember(Name = "status")]
        public string Status { get; set; }

        [DataMember(Name = "start_time")]
        public DateTime StartTime { get; set; }

        [DataMember(Name = "end_time")]
        public DateTime EndTime { get; set; }

        [DataMember(Name = "duration")]
        public string Duration
        {
            get
            {
                if (StartTime == default(DateTime))
                {
                    return null;
                }

                DateTime endTime = EndTime == default(DateTime) ? DateTime.UtcNow : EndTime;

                return (endTime - StartTime).ToString();
            }
        }

        [DataMember(Name = "output_url")]
        public Uri OutputUrl { get; set; }

        [DataMember(Name = "error_url")]
        public Uri ErrorUrl { get; set; }

        [DataMember(Name = "url")]
        public Uri Url { get; set; }
    }
}

using System;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Contracts.Jobs
{
    public class TriggeredJobRun : INamedObject
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name
        {
            get { return Id; }
        }

        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        [JsonProperty(PropertyName = "start_time")]
        public DateTime StartTime { get; set; }

        [JsonProperty(PropertyName = "end_time")]
        public DateTime EndTime { get; set; }

        [JsonProperty(PropertyName = "duration")]
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

        [JsonProperty(PropertyName = "output_url")]
        public Uri OutputUrl { get; set; }

        [JsonProperty(PropertyName = "error_url")]
        public Uri ErrorUrl { get; set; }

        [JsonProperty(PropertyName = "url")]
        public Uri Url { get; set; }

        [JsonProperty(PropertyName = "job_name")]
        public string JobName { get; set; }

        [JsonProperty(PropertyName = "trigger")]
        public string Trigger { get; set; }

        public override int GetHashCode()
        {
            return HashHelpers.CalculateCompositeHash(Id, Status, Duration);
        }
    }
}

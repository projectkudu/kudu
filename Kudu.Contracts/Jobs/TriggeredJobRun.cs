using System;
using Kudu.Contracts.Infrastructure;
using System.Text.Json.Serialization;

namespace Kudu.Contracts.Jobs
{
    public class TriggeredJobRun : INamedObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name
        {
            get { return Id; }
        }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("start_time")]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("end_time")]
        public DateTime EndTime { get; set; }

        [JsonPropertyName("duration")]
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

        [JsonPropertyName("output_url")]
        public Uri OutputUrl { get; set; }

        [JsonPropertyName("error_url")]
        public Uri ErrorUrl { get; set; }

        [JsonPropertyName("url")]
        public Uri Url { get; set; }

        [JsonPropertyName("job_name")]
        public string JobName { get; set; }

        [JsonPropertyName("trigger")]
        public string Trigger { get; set; }

        public override int GetHashCode()
        {
            return HashHelpers.CalculateCompositeHash(Id, Status, Duration);
        }
    }
}

using System;
using System.Text.Json.Serialization;

namespace Kudu.Contracts.Jobs
{
    public class ContinuousJob : JobBase
    {
        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("detailed_status")]
        public string DetailedStatus { get; set; }

        [JsonPropertyName("log_url")]
        public Uri LogUrl { get; set; }

        public override int GetHashCode()
        {
            return HashHelpers.CalculateCompositeHash(DetailedStatus, base.GetHashCode());
        }
    }
}

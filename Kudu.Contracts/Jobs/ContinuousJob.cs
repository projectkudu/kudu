using System;
using Newtonsoft.Json;

namespace Kudu.Contracts.Jobs
{
    public class ContinuousJob : JobBase
    {
        [JsonProperty(PropertyName = "status")]
        public string Status { get; set; }

        [JsonProperty(PropertyName = "detailed_status")]
        public string DetailedStatus { get; set; }

        [JsonProperty(PropertyName = "log_url")]
        public Uri LogUrl { get; set; }

        public override int GetHashCode()
        {
            return HashHelpers.CalculateCompositeHash(DetailedStatus, base.GetHashCode());
        }
    }
}

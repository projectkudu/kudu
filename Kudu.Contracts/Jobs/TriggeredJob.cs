using System;
using Newtonsoft.Json;

namespace Kudu.Contracts.Jobs
{
    public class TriggeredJob : JobBase
    {
        [JsonProperty(PropertyName = "latest_run")]
        public TriggeredJobRun LatestRun { get; set; }

        [JsonProperty(PropertyName = "history_url")]
        public Uri HistoryUrl { get; set; }

        [JsonProperty(PropertyName = "scheduler_logs_url")]
        public Uri SchedulerLogsUrl { get; set; }

        public override int GetHashCode()
        {
            return HashHelpers.CalculateCompositeHash(LatestRun, base.GetHashCode());
        }
    }
}
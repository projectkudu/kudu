using System;
using System.Text.Json.Serialization;

namespace Kudu.Contracts.Jobs
{
    public class TriggeredJob : JobBase
    {
        [JsonPropertyName("latest_run")]
        public TriggeredJobRun LatestRun { get; set; }

        [JsonPropertyName("history_url")]
        public Uri HistoryUrl { get; set; }

        [JsonPropertyName("scheduler_logs_url")]
        public Uri SchedulerLogsUrl { get; set; }

        public override int GetHashCode()
        {
            return HashHelpers.CalculateCompositeHash(LatestRun, base.GetHashCode());
        }
    }
}
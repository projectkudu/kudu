using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kudu.Contracts.Jobs
{
    public class TriggeredJobHistory
    {
        [JsonPropertyName("runs")]
        public IEnumerable<TriggeredJobRun> TriggeredJobRuns { get; set; }
    }
}
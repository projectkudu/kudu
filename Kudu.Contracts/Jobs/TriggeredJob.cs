using System;
using System.Runtime.Serialization;

namespace Kudu.Contracts.Jobs
{
    [DataContract]
    public class TriggeredJob : JobBase
    {
        [DataMember(Name = "latest_run")]
        public TriggeredJobRun LatestRun { get; set; }

        [DataMember(Name = "history_url")]
        public Uri HistoryUrl { get; set; }
    }
}
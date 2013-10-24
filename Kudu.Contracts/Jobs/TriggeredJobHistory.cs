using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Kudu.Contracts.Jobs
{
    [DataContract]
    public class TriggeredJobHistory
    {
        [DataMember(Name = "runs")]
        public IEnumerable<TriggeredJobRun> TriggeredJobRuns { get; set; }
    }
}
using System;
using System.Runtime.Serialization;

namespace Kudu.Contracts.Jobs
{
    [DataContract]
    public class ContinuousJob : JobBase
    {
        [DataMember(Name = "status")]
        public string Status { get; set; }

        [DataMember(Name = "log_url")]
        public Uri LogUrl { get; set; }
    }
}

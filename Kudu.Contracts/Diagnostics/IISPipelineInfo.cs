using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Kudu.Core.Diagnostics
{
    [DebuggerDisplay("{TotalServedRequestsCount} {ActiveRequestsCount}")]
    [DataContract(Name = "iispipeline")]
    public class IISPipelineInfo
    {
        [DataMember(Name = "total_requests_count", EmitDefaultValue = true)]
        public int TotalServedRequestsCount { get; set; }

        [DataMember(Name = "active_requests_count", EmitDefaultValue = true)]
        public int ActiveRequestsCount { get; set; }

        [DataMember(Name = "list_active_requests", EmitDefaultValue = true)]
        public IEnumerable<string> ListActiveRequests { get; set; }
    }
}

using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.Contracts.Scan
{

    public class ScanOverviewResult : INamedObject
    {
        [JsonProperty(PropertyName = "status_info")]
        public ScanStatusResult Status { get; set; }

        [JsonProperty(PropertyName = "scan_results_url")]
        public String ScanResultsUrl { get; set; }

       /* [JsonIgnore]
        public DateTime ReceivedTime { get; set; }*/

        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM spceific name")]
        string INamedObject.Name { get { return Status.Id; } }
    }
}

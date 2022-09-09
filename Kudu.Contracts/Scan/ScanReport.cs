using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.Contracts.Scan
{
    public class ScanReport : INamedObject
    {
        [JsonProperty(PropertyName = "report")]
        public ScanDetail Report { get; set; }

        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM spceific name")]
        string INamedObject.Name { get { return Id; } }

        [JsonProperty(PropertyName = "timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonIgnore]
        public String Id { get; set; }
    }
}

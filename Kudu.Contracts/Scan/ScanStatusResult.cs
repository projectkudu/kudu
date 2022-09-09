using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.Contracts.Scan
{
    public class ScanStatusResult : INamedObject
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "status")]
        [JsonConverter(typeof(StringEnumConverter))]
        public ScanStatus Status { get; set; }

        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM spceific name")]
        string INamedObject.Name { get { return Id; } }
    }
}

using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Services.Arm
{
    public class ArmEntry<T> where T : INamedObject
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        [JsonProperty(PropertyName = "location")]
        public string Location { get; set; }

        [JsonProperty(PropertyName = "properties")]
        public T Properties { get; set; }
    }
}

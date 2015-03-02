using Newtonsoft.Json;

namespace Kudu.Contracts.Infrastructure
{
    public class NamedObject : INamedObject
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
    }
}

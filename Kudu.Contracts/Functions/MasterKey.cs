using Newtonsoft.Json;

namespace Kudu.Core.Functions
{
    public class MasterKey
    {
        [JsonProperty(PropertyName = "masterKey")]
        public string Key { get; set; }
    }
}

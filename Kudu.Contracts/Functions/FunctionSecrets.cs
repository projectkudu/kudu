using Newtonsoft.Json;

namespace Kudu.Core.Functions
{
    public class FunctionSecrets
    {
        [JsonProperty(PropertyName = "key")]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "trigger_url")]
        public string TriggerUrl { get; set; }
    }
}

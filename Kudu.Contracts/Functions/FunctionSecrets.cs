using System.Text.Json.Serialization;

namespace Kudu.Core.Functions
{
    public class FunctionSecrets
    {
        [JsonPropertyName("key")]
        public string Key { get; set; }

        [JsonPropertyName("trigger_url")]
        public string TriggerUrl { get; set; }
    }
}

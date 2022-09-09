using System.Text.Json.Serialization;

namespace Kudu.Core.Functions
{
    public class MasterKey
    {
        [JsonPropertyName("masterKey")]
        public string Key { get; set; }
    }
}

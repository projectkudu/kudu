using Newtonsoft.Json;

namespace Kudu.Core.Hooks
{
    public class WebHook
    {
        public WebHook(string hookEventType, string hookAddress, string id = null, bool insecureSsl = false)
        {
            Id = id;
            HookEventType = hookEventType;
            HookAddress = hookAddress != null ? hookAddress.Trim() : string.Empty;
            InsecureSsl = insecureSsl;
        }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "event")]
        public string HookEventType { get; private set; }

        [JsonProperty(PropertyName = "url")]
        public string HookAddress { get; private set; }

        [JsonProperty(PropertyName = "insecure_ssl")]
        public bool InsecureSsl { get; private set; }
    }
}

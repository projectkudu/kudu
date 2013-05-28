using Newtonsoft.Json;

namespace Kudu.Core.Hooks
{
    public class WebHook
    {
        public WebHook(HookEventType hookEventType, string hookAddress)
        {
            HookEventType = hookEventType;
            HookAddress = hookAddress != null ? hookAddress.Trim() : string.Empty;
        }

        [JsonProperty(PropertyName = "event")]
        public HookEventType HookEventType { get; private set; }

        [JsonProperty(PropertyName = "target_url")]
        public string HookAddress { get; private set; }
    }
}

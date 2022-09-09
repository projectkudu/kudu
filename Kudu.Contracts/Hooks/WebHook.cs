using System;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("event")]
        public string HookEventType { get; private set; }

        [JsonPropertyName("url")]
        public string HookAddress { get; private set; }

        [JsonPropertyName("insecure_ssl")]
        public bool InsecureSsl { get; private set; }

        [JsonPropertyName("last_status")]
        public string LastPublishStatus { get; set; }

        [JsonPropertyName("last_reason")]
        public string LastPublishReason { get; set; }

        [JsonPropertyName("last_datetime")]
        public DateTime LastPublishDate { get; set; }

        [JsonPropertyName("last_context")]
        public string LastContext { get; set; }
    }
}

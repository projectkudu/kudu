using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using Kudu.Contracts.Infrastructure;

namespace Kudu.Core.Functions
{
    public class FunctionEnvelope : INamedObject
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("function_app_id")]
        public string FunctionAppId { get; set; }

        [JsonPropertyName("script_root_path_href")]
        public Uri ScriptRootPathHref { get; set; }

        [JsonPropertyName("script_href")]
        public Uri ScriptHref { get; set; }

        [JsonPropertyName("config_href")]
        public Uri ConfigHref { get; set; }

        [JsonPropertyName("secrets_file_href")]
        public Uri SecretsFileHref { get; set; }

        [JsonPropertyName("href")]
        public Uri Href { get; set; }

        [JsonPropertyName("config")]
        public JsonNode Config { get; set; }

        [JsonPropertyName("files")]
        public IDictionary<string, string> Files { get; set; }

        [JsonPropertyName("test_data")]
        public string TestData { get; set; }
    }
}

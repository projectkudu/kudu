using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Kudu.Contracts.Jobs
{
    public class FunctionEnvelope : INamedObject
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "script_root_path_href")]
        public Uri ScriptRootPathHref { get; set; }

        [JsonProperty(PropertyName = "script_href")]
        public Uri ScriptHref { get; set; }

        [JsonProperty(PropertyName = "config_href")]
        public Uri ConfigHref { get; set; }

        [JsonProperty(PropertyName = "test_data_href")]
        public Uri TestDataHref { get; set; }

        [JsonProperty(PropertyName = "secrets_file_href")]
        public Uri SecretsFileHref { get; set; }

        [JsonProperty(PropertyName = "href")]
        public Uri Href { get; set; }

        [JsonProperty(PropertyName = "template_id")]
        public string TemplateId { get; set; }

        [JsonProperty(PropertyName = "config")]
        public JObject Config { get; set; }
    }
}

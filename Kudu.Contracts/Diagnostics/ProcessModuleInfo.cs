using System;
using Newtonsoft.Json;


namespace Kudu.Core.Diagnostics
{
    [JsonObject()]
    public class ProcessModuleInfo
    {
        [JsonProperty(PropertyName = "base_address")]
        public string BaseAddress { get; set; }

        [JsonProperty(PropertyName = "file_name")]
        public string FileName { get; set; }

        [JsonProperty(PropertyName = "href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Href { get; set; }

        [JsonProperty(PropertyName = "file_path", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FilePath { get; set; }

        [JsonProperty(PropertyName = "module_memory_size", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int ModuleMemorySize { get; set; }

        [JsonProperty(PropertyName = "file_version")]
        public string FileVersion { get; set; }

        [JsonProperty(PropertyName = "file_description", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FileDescription { get; set; }

        [JsonProperty(PropertyName = "product", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Product { get; set; }

        [JsonProperty(PropertyName = "product_version", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string ProductVersion { get; set; }

        [JsonProperty(PropertyName = "is_debug", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool? IsDebug { get; set; }

        [JsonProperty(PropertyName = "language", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Language { get; set; }
    }
}

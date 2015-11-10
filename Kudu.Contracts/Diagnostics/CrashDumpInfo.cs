using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Contracts.Diagnostics
{
    public class CrashDumpInfo
    {
        [JsonProperty(PropertyName = "name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "timestamp", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime Timestamp { get; set; }

        [JsonProperty(PropertyName = "file_path", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FilePath { get; set; }

        [JsonProperty(PropertyName = "href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Href { get; set; }

        [JsonProperty(PropertyName = "analyize_href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri AnalyizeHref { get; set; }

        [JsonProperty(PropertyName = "download_href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri DownloadHref { get; set; }
    }
}

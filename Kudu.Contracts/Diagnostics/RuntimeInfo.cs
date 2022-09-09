using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kudu.Services.Diagnostics
{
    public class RuntimeInfo
    {
        [JsonPropertyName("nodejs")]
        public IEnumerable<Dictionary<string, string>> NodeVersions { get; set; }

        public object DotNetCore32 { get; set; }
        public object DotNetCore64 { get; set; }
        public object AspNetCoreModule { get; set; }

        public object System { get; set; }
    }
}

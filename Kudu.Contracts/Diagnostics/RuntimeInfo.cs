using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kudu.Services.Diagnostics
{
    public class RuntimeInfo
    {
        [JsonProperty(PropertyName = "nodejs")]
        public IEnumerable<Dictionary<string, string>> NodeVersions { get; set; }

        public object DotNetCore32 { get; set; }
        public object DotNetCore64 { get; set; }
        public object AspNetCoreModule { get; set; }

        public object System { get; set; }
    }
}

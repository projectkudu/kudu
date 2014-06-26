using System.Collections.Generic;
using Newtonsoft.Json;

namespace Kudu.Services.Diagnostics
{
    public class RuntimeInfo
    {
        [JsonProperty(PropertyName = "nodejs")]
        public IEnumerable<Dictionary<string, string>> NodeVersions { get; set; }
    }
}

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Kudu.Services.Diagnostics
{
    public class DotNetCoreSharedFrameworksInfo
    {
        [JsonProperty(PropertyName = "Microsoft.NetCore.App")]
        public IEnumerable<string> NetCoreApp { get; set; }

        [JsonProperty(PropertyName = "Microsoft.AspNetCore.App")]
        public IEnumerable<string> AspNetCoreApp { get; set; }

        [JsonProperty(PropertyName = "Microsoft.AspNetCore.All")]
        public IEnumerable<string> AspNetCoreAll { get; set; }
    }
}

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Kudu.Services.Diagnostics
{
    public class DotNetCoreSharedFrameworksInfo
    {
        [JsonPropertyName("Microsoft.NetCore.App")]
        public IEnumerable<string> NetCoreApp { get; set; }

        [JsonPropertyName("Microsoft.AspNetCore.App")]
        public IEnumerable<string> AspNetCoreApp { get; set; }

        [JsonPropertyName("Microsoft.AspNetCore.All")]
        public IEnumerable<string> AspNetCoreAll { get; set; }
    }
}

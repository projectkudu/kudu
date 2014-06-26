using System;
using Newtonsoft.Json;

namespace Kudu.Core.SourceControl
{
    public class RepositoryInfo
    {
        [JsonProperty(PropertyName = "repository_type")]
        public RepositoryType Type { get; set; }

        [JsonProperty(PropertyName = "git_url")]
        public Uri GitUrl { get; set; }
    }
}

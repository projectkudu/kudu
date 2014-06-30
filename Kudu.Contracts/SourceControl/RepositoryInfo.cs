using System;
using Newtonsoft.Json;

namespace Kudu.Core.SourceControl
{
    public class RepositoryInfo
    {
        // Omitting the JsonProperty's to avoid a breaking change. We previously had [DataMember] attribs
        // but they were not working because there was no [DataContract] on the class
        //[JsonProperty(PropertyName = "repository_type")]
        public RepositoryType Type { get; set; }

        //[JsonProperty(PropertyName = "git_url")]
        public Uri GitUrl { get; set; }
    }
}

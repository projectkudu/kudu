using System;
using System.Runtime.Serialization;

namespace Kudu.Core.SourceControl
{
    public class RepositoryInfo
    {
        [DataMember(Name = "repository_type")]
        public RepositoryType Type { get; set; }

        [DataMember(Name = "git_url")]
        public Uri GitUrl { get; set; }
    }
}

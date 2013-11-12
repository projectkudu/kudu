using System;
using System.Runtime.Serialization;

namespace Kudu.Contracts.Jobs
{
    [DataContract]
    public abstract class JobBase
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "run_command")]
        public string ScriptFilePath { get; set; }

        [DataMember(Name = "url")]
        public Uri Url { get; set; }

        [DataMember(Name = "extra_info_url")]
        public Uri ExtraInfoUrl { get; set; }

        [DataMember(Name = "type")]
        public string JobType { get; set; }

        public IScriptHost ScriptHost { get; set; }
    }
}

using System;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;
using System.Xml.Linq;

namespace Kudu.Contracts.Jobs
{
    public abstract class JobBase : INamedObject
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("run_command")]
        public string RunCommand { get; set; }

        [JsonPropertyName("url")]
        public Uri Url { get; set; }

        [JsonPropertyName("extra_info_url")]
        public Uri ExtraInfoUrl { get; set; }

        [JsonPropertyName("type")]
        public string JobType { get; set; }

        [JsonPropertyName("error")]
        public string Error { get; set; }

        [JsonPropertyName("using_sdk")]
        public bool UsingSdk { get; set; }

        [JsonPropertyName("settings")]
        public JobSettings Settings { get; set; }

        [JsonIgnore]
        public IScriptHost ScriptHost { get; set; }

        [JsonIgnore]
        public string ScriptFilePath { get; set; }

        [JsonIgnore]
        public string JobBinariesRootPath { get; set; }

        [JsonIgnore]
        public string CommandArguments { get; set; }

        public override int GetHashCode()
        {
            return HashHelpers.CalculateCompositeHash(Name, RunCommand, JobType, Error);
        }
    }
}

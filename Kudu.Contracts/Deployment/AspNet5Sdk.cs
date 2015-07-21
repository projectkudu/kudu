using Newtonsoft.Json;

namespace Kudu.Core.Deployment
{
    public class AspNet5Sdk
    {
        [JsonProperty(PropertyName = "version")]
        public string Version { get; set; }

        [JsonProperty(PropertyName = "runtime")]
        public string Runtime { get; set; }

        [JsonProperty(PropertyName = "architecture")]
        public string Architecture { get; set; }

        public AspNet5Sdk()
        {
            Version = Constants.DnxDefaultVersion;
            Runtime = Constants.DnxDefaultClr;
        }
    }
}

using Newtonsoft.Json;
using System.Collections.Generic;

namespace Kudu.Contracts.Jobs
{
    public class FunctionTemplate
    {
        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "trigger")]
        public string Trigger { get; set; }

        [JsonProperty(PropertyName = "inputs")]
        public IEnumerable<FunctionTemplateInput> Inputs { get; set; }

        [JsonProperty(PropertyName = "language")]
        public string Language { get; set; }
    }

    public class FunctionTemplateInput
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "input_type")]
        public string InputType { get; set; }
    }
}

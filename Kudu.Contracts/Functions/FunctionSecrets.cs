using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;

namespace Kudu.Core.Functions
{
    public class FunctionSecrets
    {
        [JsonProperty(PropertyName = "key")]
        public string Key { get; set; }

        [JsonProperty(PropertyName = "trigger_url")]
        public string TriggerUrl { get; set; }
    }
}

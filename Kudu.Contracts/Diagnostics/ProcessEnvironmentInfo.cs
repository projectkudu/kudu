using System.Collections.Generic;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Core.Diagnostics
{
    public class ProcessEnvironmentInfo : Dictionary<string, string>, INamedObject
    {
        private readonly string _name;

        public ProcessEnvironmentInfo()
        {
        }

        public ProcessEnvironmentInfo(string name, Dictionary<string, string> values)
            : base(values)
        {
            _name = name;
        }

        [JsonProperty(PropertyName = "name")]
        string INamedObject.Name { get { return _name; } }
    }
}
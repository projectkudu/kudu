using System.Collections.Generic;
using System.Text.Json.Serialization;
using Kudu.Contracts.Infrastructure;

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

        [JsonPropertyName("name")]
        string INamedObject.Name { get { return _name; } }
    }
}
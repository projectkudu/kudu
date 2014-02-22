using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Kudu.Services.Diagnostics
{
    [DataContract(Name = "runtime")]
    public class RuntimeInfo
    {
        [DataMember(Name = "nodejs")]
        public IEnumerable<Dictionary<string, string>> NodeVersions { get; set; }
    }
}

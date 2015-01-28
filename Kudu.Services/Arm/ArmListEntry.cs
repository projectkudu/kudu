using System.Collections.Generic;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Services.Arm
{
    public class ArmListEntry<T> where T : INamedObject
    {
        [JsonProperty(PropertyName = "value")]
        public IEnumerable<ArmEntry<T>> Value { get; set; }
    }
}

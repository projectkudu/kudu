using System.Runtime.Serialization;

namespace Kudu.Contracts.Jobs
{
    [DataContract]
    public class ContinuousJobSettings
    {
        [DataMember(Name = "is_singleton")]
        public bool IsSingleton { get; set; }
    }
}

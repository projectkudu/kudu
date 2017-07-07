using System;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Core.Scaling
{
    public sealed class WorkerInfo : INamedObject
    {
        string INamedObject.Name
        {
            get
            {
                return this.Id;
            }
        }
        [JsonProperty(PropertyName = "id")]
        public string Id
        {
            get;
            set;
        }
        [JsonProperty(PropertyName = "stampName")]
        public string StampName
        {
            get;
            set;
        }
        [JsonProperty(PropertyName = "workerName")]
        public string WorkerName
        {
            get;
            set;
        }
        [JsonProperty(PropertyName = "loadFactor")]
        public string LoadFactor
        {
            get;
            set;
        }
        [JsonProperty(PropertyName = "lastModifiedTimeUtc")]
        public string LastModifiedTimeUtc
        {
            get;
            set;
        }
        [JsonProperty(PropertyName = "isManager")]
        public bool IsManager
        {
            get;
            set;
        }
        [JsonProperty(PropertyName = "isStale")]
        public bool IsStale
        {
            get;
            set;
        }
    }
}

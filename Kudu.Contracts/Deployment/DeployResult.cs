using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Core.Deployment
{
    [DebuggerDisplay("{Id} {Status}")]
    public class DeployResult : INamedObject
    {
        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM spceific name")]
        string INamedObject.Name { get { return Id; } }

        [JsonProperty(PropertyName = "id")]
        public string Id { get; set; }

        [JsonProperty(PropertyName = "status")]
        public DeployStatus Status { get; set; }

        [JsonProperty(PropertyName = "status_text")]
        public string StatusText { get; set; }

        [JsonProperty(PropertyName = "author_email")]
        public string AuthorEmail { get; set; }

        [JsonProperty(PropertyName = "author")]
        public string Author { get; set; }

        [JsonProperty(PropertyName = "deployer")]
        public string Deployer { get; set; }

        [JsonProperty(PropertyName = "message")]
        public string Message { get; set; }

        [JsonProperty(PropertyName = "progress")]
        public string Progress { get; set; }

        [JsonProperty(PropertyName = "received_time")]
        public DateTime ReceivedTime { get; set; }

        [JsonProperty(PropertyName = "start_time")]
        public DateTime StartTime { get; set; }

        [JsonProperty(PropertyName = "end_time")]
        public DateTime? EndTime { get; set; }

        [JsonProperty(PropertyName = "last_success_end_time")]
        public DateTime? LastSuccessEndTime { get; set; }

        [JsonProperty(PropertyName = "complete")]
        public bool Complete { get; set; }

        [JsonProperty(PropertyName = "active")]
        public bool Current { get; set; }

        [JsonProperty(PropertyName = "is_temp")]
        public bool IsTemporary { get; set; }

        [JsonProperty(PropertyName = "is_readonly")]
        public bool IsReadOnly { get; set; }

        [JsonProperty(PropertyName = "url")]
        public Uri Url { get; set; }

        [JsonProperty(PropertyName = "log_url")]
        public Uri LogUrl { get; set; }

        [JsonProperty(PropertyName = "site_name")]
        public string SiteName { get; set; }
    }
}
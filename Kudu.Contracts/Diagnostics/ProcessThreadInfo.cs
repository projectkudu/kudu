using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Kudu.Contracts.Infrastructure;

namespace Kudu.Core.Diagnostics
{
    public class ProcessThreadInfo : INamedObject
    {
        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM specific name")]
        string INamedObject.Name { get { return Id.ToString(); } }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("href"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uri Href { get; set; }

        [JsonPropertyName("process"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uri Process { get; set; }

        [JsonPropertyName("start_address"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string StartAddress { get; set; }

        [JsonPropertyName("current_priority"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int CurrentPriority { get; set; }

        [JsonPropertyName("priority_level"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string PriorityLevel { get; set; }

        [JsonPropertyName("base_priority"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int BasePriority { get; set; }

        [JsonPropertyName("start_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime StartTime { get; set; }

        [JsonPropertyName("total_processor_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan TotalProcessorTime { get; set; }

        [JsonPropertyName("user_processor_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan UserProcessorTime { get; set; }

        [JsonPropertyName("priviledged_processor_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan PriviledgedProcessorTime { get; set; }

        [JsonPropertyName("state"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string State { get; set; }

        [JsonPropertyName("wait_reason"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string WaitReason { get; set; }
    }
}
﻿using System;
using System.Diagnostics.CodeAnalysis;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Core.Diagnostics
{
    public class ProcessThreadInfo : INamedObject
    {
        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM spceific name")]
        string INamedObject.Name { get { return Id.ToString(); } }

        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }

        [JsonProperty(PropertyName = "href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Href { get; set; }

        [JsonProperty(PropertyName = "process", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Process { get; set; }

        [JsonProperty(PropertyName = "start_address", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string StartAddress { get; set; }

        [JsonProperty(PropertyName = "current_priority", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int CurrentPriority { get; set; }

        [JsonProperty(PropertyName = "priority_level", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string PriorityLevel { get; set; }

        [JsonProperty(PropertyName = "base_priority", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int BasePriority { get; set; }

        [JsonProperty(PropertyName = "start_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime StartTime { get; set; }

        [JsonProperty(PropertyName = "total_processor_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan TotalProcessorTime { get; set; }

        [JsonProperty(PropertyName = "user_processor_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan UserProcessorTime { get; set; }

        [JsonProperty(PropertyName = "priviledged_processor_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan PriviledgedProcessorTime { get; set; }

        [JsonProperty(PropertyName = "state", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string State { get; set; }

        [JsonProperty(PropertyName = "wait_reason", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string WaitReason { get; set; }
    }
}
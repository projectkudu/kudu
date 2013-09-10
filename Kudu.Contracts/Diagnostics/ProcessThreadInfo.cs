﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Kudu.Core.Diagnostics
{
    [DataContract(Name = "processthread")]
    public class ProcessThreadInfo
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "href", EmitDefaultValue = false)]
        public Uri Href { get; set; }

        [DataMember(Name = "process", EmitDefaultValue = false)]
        public Uri Process { get; set; }

        [DataMember(Name = "start_address", EmitDefaultValue = false)]
        public string StartAddress { get; set; }

        [DataMember(Name = "current_priority", EmitDefaultValue = false)]
        public int CurrentPriority { get; set; }

        [DataMember(Name = "priority_level", EmitDefaultValue = false)]
        public string PriorityLevel { get; set; }

        [DataMember(Name = "base_priority", EmitDefaultValue = false)]
        public int BasePriority { get; set; }

        [DataMember(Name = "start_time", EmitDefaultValue = false)]
        public DateTime StartTime { get; set; }

        [DataMember(Name = "total_processor_time", EmitDefaultValue = false)]
        public TimeSpan TotalProcessorTime { get; set; }

        [DataMember(Name = "user_processor_time", EmitDefaultValue = false)]
        public TimeSpan UserProcessorTime { get; set; }

        [DataMember(Name = "priviledged_processor_time", EmitDefaultValue = false)]
        public TimeSpan PriviledgedProcessorTime { get; set; }

        [DataMember(Name = "state", EmitDefaultValue = false)]
        public string State { get; set; }

        [DataMember(Name = "wait_reason", EmitDefaultValue = false)]
        public string WaitReason { get; set; }
    }
}
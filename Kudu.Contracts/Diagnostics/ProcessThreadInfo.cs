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

        [DataMember(Name = "startaddress", EmitDefaultValue = false)]
        public string StartAddress { get; set; }

        [DataMember(Name = "currentpriority", EmitDefaultValue = false)]
        public int CurrentPriority { get; set; }

        [DataMember(Name = "prioritylevel", EmitDefaultValue = false)]
        public string PriorityLevel { get; set; }

        [DataMember(Name = "basepriority", EmitDefaultValue = false)]
        public int BasePriority { get; set; }

        [DataMember(Name = "starttime", EmitDefaultValue = false)]
        public DateTime StartTime { get; set; }

        [DataMember(Name = "totalprocessortime", EmitDefaultValue = false)]
        public TimeSpan TotalProcessorTime { get; set; }

        [DataMember(Name = "userprocessortime", EmitDefaultValue = false)]
        public TimeSpan UserProcessorTime { get; set; }

        [DataMember(Name = "priviledgedprocessortime", EmitDefaultValue = false)]
        public TimeSpan PriviledgedProcessorTime { get; set; }

        [DataMember(Name = "state", EmitDefaultValue = false)]
        public string State { get; set; }

        [DataMember(Name = "waitreason", EmitDefaultValue = false)]
        public string WaitReason { get; set; }
    }
}
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Kudu.Core.Diagnostics
{
    [DebuggerDisplay("{Id} {Name}")]
    [DataContract(Name = "process")]
    public class ProcessInfo
    {
        [DataMember(Name = "id")]
        public int Id { get; set; }

        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "href", EmitDefaultValue = false)]
        public Uri Href { get; set; }

        [DataMember(Name = "minidump", EmitDefaultValue = false)]
        public Uri MiniDump { get; set; }

        [DataMember(Name = "parent", EmitDefaultValue = false)]
        public Uri Parent { get; set; }

        [DataMember(Name = "children", EmitDefaultValue = false)]
        public IEnumerable<Uri> Children { get; set; }

        [DataMember(Name = "file_name", EmitDefaultValue = false)]
        public string FileName { get; set; }

        //[DataMember(Name = "arguments", EmitDefaultValue = false)]
        //public string Arguments { get; set; }

        //[DataMember(Name = "username", EmitDefaultValue = false)]
        //public string UserName { get; set; }

        [DataMember(Name = "handle_count", EmitDefaultValue = false)]
        public int HandleCount { get; set; }

        [DataMember(Name = "module_count", EmitDefaultValue = false)]
        public int ModuleCount { get; set; }

        [DataMember(Name = "thread_count", EmitDefaultValue = false)]
        public int ThreadCount { get; set; }

        [DataMember(Name = "start_time", EmitDefaultValue = false)]
        public DateTime StartTime { get; set; }

        [DataMember(Name = "total_cpu_time", EmitDefaultValue = false)]
        public TimeSpan TotalProcessorTime { get; set; }

        [DataMember(Name = "user_cpu_time", EmitDefaultValue = false)]
        public TimeSpan UserProcessorTime { get; set; }

        [DataMember(Name = "privileged_cpu_time", EmitDefaultValue = false)]
        public TimeSpan PrivilegedProcessorTime { get; set; }

        [DataMember(Name = "working_set", EmitDefaultValue = false)]
        public Int64 WorkingSet64 { get; set; }

        [DataMember(Name = "peak_working_set", EmitDefaultValue = false)]
        public Int64 PeakWorkingSet64 { get; set; }

        [DataMember(Name = "private_working_set", EmitDefaultValue = false)]
        public Int64 PrivateWorkingSet64 { get; set; }

        [DataMember(Name = "private_memory", EmitDefaultValue = false)]
        public Int64 PrivateMemorySize64 { get; set; }

        [DataMember(Name = "virtual_memory", EmitDefaultValue = false)]
        public Int64 VirtualMemorySize64 { get; set; }

        [DataMember(Name = "peak_virtual_memory", EmitDefaultValue = false)]
        public Int64 PeakVirtualMemorySize64 { get; set; }

        [DataMember(Name = "paged_system_memory", EmitDefaultValue = false)]
        public Int64 PagedSystemMemorySize64 { get; set; }

        [DataMember(Name = "non_paged_system_memory", EmitDefaultValue = false)]
        public Int64 NonpagedSystemMemorySize64 { get; set; }

        [DataMember(Name = "paged_memory", EmitDefaultValue = false)]
        public Int64 PagedMemorySize64 { get; set; }

        [DataMember(Name = "peak_paged_memory", EmitDefaultValue = false)]
        public Int64 PeakPagedMemorySize64 { get; set; }
    }
}
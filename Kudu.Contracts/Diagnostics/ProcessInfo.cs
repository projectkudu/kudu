using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Core.Diagnostics
{
    [DebuggerDisplay("{Id} {Name}")]
    public class ProcessInfo : INamedObject
    {
        [JsonProperty(PropertyName = "id")]
        public int Id { get; set; }

        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM spceific name")]
        string INamedObject.Name { get { return Id.ToString(); } }

        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "href", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Href { get; set; }

        [JsonProperty(PropertyName = "minidump", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri MiniDump { get; set; }

        [JsonProperty(PropertyName = "is_profile_running", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsProfileRunning { get; set; }

        [JsonProperty(PropertyName = "is_iis_profile_running", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsIisProfileRunning { get; set; }

        [JsonProperty(PropertyName = "iis_profile_timeout_in_seconds", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public double IisProfileTimeoutInSeconds { get; set; }

        [JsonProperty(PropertyName = "parent", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Uri Parent { get; set; }

        [JsonProperty(PropertyName = "children", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<Uri> Children { get; set; }

        [JsonProperty(PropertyName = "threads", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<ProcessThreadInfo> Threads { get; set; }

        [JsonProperty(PropertyName = "open_file_handles", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<string> OpenFileHandles { get; set; }

        [JsonProperty(PropertyName = "modules", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public IEnumerable<ProcessModuleInfo> Modules { get; set; }

        [JsonProperty(PropertyName = "file_name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string FileName { get; set; }

        [JsonProperty(PropertyName = "command_line", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string CommandLine { get; set; }

        //[JsonProperty(PropertyName = "arguments", DefaultValueHandling = DefaultValueHandling.Ignore)]
        //public string Arguments { get; set; }

        [JsonProperty(PropertyName = "user_name", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "handle_count", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int HandleCount { get; set; }

        [JsonProperty(PropertyName = "module_count", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int ModuleCount { get; set; }

        [JsonProperty(PropertyName = "thread_count", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public int ThreadCount { get; set; }

        [JsonProperty(PropertyName = "start_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime StartTime { get; set; }

        [JsonProperty(PropertyName = "total_cpu_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan TotalProcessorTime { get; set; }

        [JsonProperty(PropertyName = "user_cpu_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan UserProcessorTime { get; set; }

        [JsonProperty(PropertyName = "privileged_cpu_time", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public TimeSpan PrivilegedProcessorTime { get; set; }

        [JsonProperty(PropertyName = "working_set", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 WorkingSet64 { get; set; }

        [JsonProperty(PropertyName = "peak_working_set", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PeakWorkingSet64 { get; set; }

        [JsonProperty(PropertyName = "private_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PrivateMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "virtual_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 VirtualMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "peak_virtual_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PeakVirtualMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "paged_system_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PagedSystemMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "non_paged_system_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 NonpagedSystemMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "paged_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PagedMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "peak_paged_memory", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Int64 PeakPagedMemorySize64 { get; set; }

        [JsonProperty(PropertyName = "time_stamp", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public DateTime TimeStamp { get; set; }

        [JsonProperty(PropertyName = "environment_variables", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public Dictionary<string, string> EnvironmentVariables { get; set; }

        [JsonProperty(PropertyName = "is_scm_site", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsScmSite { get; set; }

        [JsonProperty(PropertyName = "is_webjob", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public bool IsWebJob { get; set; }

        [JsonProperty(PropertyName = "description", DefaultValueHandling = DefaultValueHandling.Ignore)]
        public string Description { get; set; }
    }
}
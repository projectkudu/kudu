using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Kudu.Contracts.Infrastructure;
using System.Text.Json.Serialization;

namespace Kudu.Core.Diagnostics
{
    [DebuggerDisplay("{Id} {Name}")]
    public class ProcessInfo : INamedObject
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [Newtonsoft.Json.JsonIgnore]
        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM specific name")]
        string INamedObject.Name { get { return Id.ToString(); } }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("machineName")]
        public string MachineName { get; set; }

        // For now, we have json properties for both Netwonsoft and System.Text json implementations
        // Both are needed depending on if these are called by the Kudu ContainerServices Agent or standard Kudu
        [Newtonsoft.Json.JsonProperty(PropertyName = "href", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("href"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uri Href { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "minidump", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("minidump"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uri MiniDump { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "is_profile_running", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("is_profile_running"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsProfileRunning { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "is_iis_profile_running", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("is_iis_profile_running"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsIisProfileRunning { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "iis_profile_timeout_in_seconds", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("iis_profile_timeout_in_seconds"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public double IisProfileTimeoutInSeconds { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "parent", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("parent"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uri Parent { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "children", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("children"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IEnumerable<Uri> Children { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "threads", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("threads"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IEnumerable<ProcessThreadInfo> Threads { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "open_file_handles", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("open_file_handles"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IEnumerable<string> OpenFileHandles { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "modules", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]        
        [JsonPropertyName("modules"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public IEnumerable<ProcessModuleInfo> Modules { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "file_name", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]        
        [JsonPropertyName("file_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string FileName { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "command_line", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("command_line"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string CommandLine { get; set; }

        //[JsonProperty(PropertyName = "arguments", DefaultValueHandling = DefaultValueHandling.Ignore)]
        //public string Arguments { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "user_name", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("user_name"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string UserName { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "handle_count", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]        
        [JsonPropertyName("handle_count"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int HandleCount { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "module_count", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]        
        [JsonPropertyName("module_count"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ModuleCount { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "thread_count", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("thread_count"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ThreadCount { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "start_time", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("start_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime StartTime { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "total_cpu_time", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("total_cpu_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan TotalProcessorTime { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "user_cpu_time", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("user_cpu_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan UserProcessorTime { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "privileged_cpu_time", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("privileged_cpu_time"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public TimeSpan PrivilegedProcessorTime { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "working_set", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("working_set"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long WorkingSet64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "peak_working_set", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("peak_working_set"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PeakWorkingSet64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "private_memory", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("private_memory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PrivateMemorySize64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "virtual_memory", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("virtual_memory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long VirtualMemorySize64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "peak_virtual_memory", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("peak_virtual_memory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PeakVirtualMemorySize64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "paged_system_memory", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("paged_system_memory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PagedSystemMemorySize64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "non_paged_system_memory", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("non_paged_system_memory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long NonpagedSystemMemorySize64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "paged_memory", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("paged_memory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PagedMemorySize64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "peak_paged_memory", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("peak_paged_memory"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public long PeakPagedMemorySize64 { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "time_stamp", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("time_stamp"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public DateTime TimeStamp { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "environment_variables", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("environment_variables"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Dictionary<string, string> EnvironmentVariables { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "is_scm_site", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("is_scm_site"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsScmSite { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "is_webjob", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("is_webjob"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool IsWebJob { get; set; }

        [Newtonsoft.Json.JsonProperty(PropertyName = "description", DefaultValueHandling = Newtonsoft.Json.DefaultValueHandling.Ignore)]
        [JsonPropertyName("description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Description { get; set; }
    }
}
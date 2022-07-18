//------------------------------------------------------------------------------
// <copyright file="ProcessInfo.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Kudu.Agent.Contracts
{
    [DataContract]
    [DebuggerDisplay("{Id} {Name}")]    
    public class ProcessInfo
    {
        [DataMember(Name ="id")]
        public int Id { get; set; }

        [DataMember(Name ="name")]
        public string Name { get; set; }

        [DataMember(Name ="machineName")]
        public string MachineName { get; set; }

        [DataMember(Name ="minidump", EmitDefaultValue = false)]
        public Uri MiniDump { get; set; }

        [DataMember(Name ="is_profile_running", EmitDefaultValue = false)]
        public bool IsProfileRunning { get; set; }

        [DataMember(Name ="is_iis_profile_running", EmitDefaultValue = false)]
        public bool IsIisProfileRunning { get; set; }

        [DataMember(Name ="iis_profile_timeout_in_seconds", EmitDefaultValue = false)]
        public double IisProfileTimeoutInSeconds { get; set; }

        [DataMember(Name ="parent", EmitDefaultValue = false)]
        public int ParentId { get; set; }

        [DataMember(Name ="children", EmitDefaultValue = false)]
        public IEnumerable<int> Children { get; set; }

        [DataMember(Name ="threads", EmitDefaultValue = false)]
        public IEnumerable<ProcessThreadInfo> Threads { get; set; }

        [DataMember(Name ="open_file_handles", EmitDefaultValue = false)]
        public IEnumerable<string> OpenFileHandles { get; set; }

        [DataMember(Name ="modules", EmitDefaultValue = false)]
        public IEnumerable<ProcessModuleInfo> Modules { get; set; }

        [DataMember(Name ="file_name", EmitDefaultValue = false)]
        public string FileName { get; set; }

        [DataMember(Name ="command_line", EmitDefaultValue = false)]
        public string CommandLine { get; set; }

        [DataMember(Name ="user_name", EmitDefaultValue = false)]
        public string UserName { get; set; }

        [DataMember(Name ="handle_count", EmitDefaultValue = false)]
        public int HandleCount { get; set; }

        [DataMember(Name ="module_count", EmitDefaultValue = false)]
        public int ModuleCount { get; set; }

        [DataMember(Name ="thread_count", EmitDefaultValue = false)]
        public int ThreadCount { get; set; }

        [DataMember(Name ="start_time", EmitDefaultValue = false)]
        public DateTime StartTime { get; set; }

        [DataMember(Name ="total_cpu_time", EmitDefaultValue = false)]
        public TimeSpan TotalProcessorTime { get; set; }

        [DataMember(Name ="user_cpu_time", EmitDefaultValue = false)]
        public TimeSpan UserProcessorTime { get; set; }

        [DataMember(Name ="privileged_cpu_time", EmitDefaultValue = false)]
        public TimeSpan PrivilegedProcessorTime { get; set; }

        [DataMember(Name ="working_set", EmitDefaultValue = false)]
        public Int64 WorkingSet64 { get; set; }

        [DataMember(Name ="peak_working_set", EmitDefaultValue = false)]
        public Int64 PeakWorkingSet64 { get; set; }

        [DataMember(Name ="private_memory", EmitDefaultValue = false)]
        public Int64 PrivateMemorySize64 { get; set; }

        [DataMember(Name ="virtual_memory", EmitDefaultValue = false)]
        public Int64 VirtualMemorySize64 { get; set; }

        [DataMember(Name ="peak_virtual_memory", EmitDefaultValue = false)]
        public Int64 PeakVirtualMemorySize64 { get; set; }

        [DataMember(Name ="paged_system_memory", EmitDefaultValue = false)]
        public Int64 PagedSystemMemorySize64 { get; set; }

        [DataMember(Name ="non_paged_system_memory", EmitDefaultValue = false)]
        public Int64 NonpagedSystemMemorySize64 { get; set; }

        [DataMember(Name ="paged_memory", EmitDefaultValue = false)]
        public Int64 PagedMemorySize64 { get; set; }

        [DataMember(Name ="peak_paged_memory", EmitDefaultValue = false)]
        public Int64 PeakPagedMemorySize64 { get; set; }

        [DataMember(Name ="time_stamp", EmitDefaultValue = false)]
        public DateTime TimeStamp { get; set; }

        [DataMember(Name ="environment_variables", EmitDefaultValue = false)]
        public Dictionary<string, string> EnvironmentVariables { get; set; }

        [DataMember(Name ="is_scm_site", EmitDefaultValue = false)]
        public bool IsScmSite { get; set; }

        [DataMember(Name ="is_webjob", EmitDefaultValue = false)]
        public bool IsWebJob { get; set; }

        [DataMember(Name ="description", EmitDefaultValue = false)]
        public string Description { get; set; }
    }
}
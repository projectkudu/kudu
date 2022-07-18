//------------------------------------------------------------------------------
// <copyright file="ProcessModuleInfo.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using System;
using System.Runtime.Serialization;

namespace Kudu.Agent.Contracts
{
    [DataContract]
    public class ProcessModuleInfo
    {
        [DataMember(Name ="base_address")]
        public string BaseAddress { get; set; }

        [DataMember(Name ="file_name")]
        public string FileName { get; set; }

        [DataMember(Name ="file_path", EmitDefaultValue = false)]
        public string FilePath { get; set; }

        [DataMember(Name ="module_memory_size", EmitDefaultValue = false)]
        public int ModuleMemorySize { get; set; }

        [DataMember(Name ="file_version")]
        public string FileVersion { get; set; }

        [DataMember(Name ="file_description", EmitDefaultValue = false)]
        public string FileDescription { get; set; }

        [DataMember(Name ="product", EmitDefaultValue = false)]
        public string Product { get; set; }

        [DataMember(Name ="product_version", EmitDefaultValue = false)]
        public string ProductVersion { get; set; }

        [DataMember(Name ="is_debug", EmitDefaultValue = false)]
        public bool? IsDebug { get; set; }

        [DataMember(Name ="language", EmitDefaultValue = false)]
        public string Language { get; set; }
    }
}

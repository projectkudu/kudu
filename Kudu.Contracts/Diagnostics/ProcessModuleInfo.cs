using System;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Kudu.Contracts.Infrastructure;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Kudu.Core.Diagnostics
{
    public class ProcessModuleInfo : INamedObject
    {
        [JsonIgnore]
        [SuppressMessage("Microsoft.Design", "CA1033:InterfaceMethodsShouldBeCallableByChildTypes", Justification = "to provide ARM specific name")]
        string INamedObject.Name { get { return BaseAddress; } }

        [JsonPropertyName("base_address")]
        public string BaseAddress { get; set; }

        [JsonPropertyName("file_name")]
        public string FileName { get; set; }

        [JsonPropertyName("href"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public Uri Href { get; set; }

        [JsonPropertyName("file_path"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string FilePath { get; set; }

        [JsonPropertyName("module_memory_size"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public int ModuleMemorySize { get; set; }

        [JsonPropertyName("file_version")]
        public string FileVersion { get; set; }

        [JsonPropertyName("file_description"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string FileDescription { get; set; }

        [JsonPropertyName("product"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Product { get; set; }

        [JsonPropertyName("product_version"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string ProductVersion { get; set; }

        [JsonPropertyName("is_debug"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public bool? IsDebug { get; set; }

        [JsonPropertyName("language"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public string Language { get; set; }
    }
}

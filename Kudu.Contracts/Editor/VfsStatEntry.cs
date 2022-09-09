using System;
using Newtonsoft.Json;

namespace Kudu.Contracts.Editor
{
    /// <summary>
    /// Represents a directory structure. Used by <see cref="VfsControllerBase"/> to browse
    /// a Kudu file system or the git repository.
    /// </summary>
    public class VfsStatEntry
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "size")]
        public long Size { get; set; }

        [JsonProperty(PropertyName = "mtime")]
        public DateTimeOffset MTime { get; set; }

        [JsonProperty(PropertyName = "crtime")]
        public DateTimeOffset CRTime { get; set; }

        [JsonProperty(PropertyName = "mime")]
        public string Mime { get; set; }

        [JsonProperty(PropertyName = "href")]
        public string Href { get; set; }

        [JsonProperty(PropertyName = "path")]
        public string Path { get; set; }
    }
}

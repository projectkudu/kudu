using System;
using System.Text.Json.Serialization;

namespace Kudu.Contracts.Editor
{
    /// <summary>
    /// Represents a directory structure. Used by <see cref="VfsControllerBase"/> to browse
    /// a Kudu file system or the git repository.
    /// </summary>
    public class VfsStatEntry
    {
        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("mtime")]
        public DateTimeOffset MTime { get; set; }

        [JsonPropertyName("crtime")]
        public DateTimeOffset CRTime { get; set; }

        [JsonPropertyName("mime")]
        public string Mime { get; set; }

        [JsonPropertyName("href")]
        public string Href { get; set; }

        [JsonPropertyName("path")]
        public string Path { get; set; }
    }
}

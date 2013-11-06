using System;
using System.Runtime.Serialization;

namespace Kudu.Contracts.Editor
{
    /// <summary>
    /// Represents a directory structure. Used by <see cref="VfsControllerBase"/> to browse
    /// a Kudu file system or the git repository.
    /// </summary>
    [DataContract]
    public class VfsStatEntry
    {
        [DataMember(Name = "name")]
        public string Name { get; set; }

        [DataMember(Name = "size")]
        public long Size { get; set; }

        [DataMember(Name = "mtime")]
        public DateTimeOffset MTime { get; set; }

        [DataMember(Name = "mime")]
        public string Mime { get; set; }

        [DataMember(Name = "href")]
        public string Href { get; set; }

        [DataMember(Name = "path")]
        public string Path { get; set; }
    }
}

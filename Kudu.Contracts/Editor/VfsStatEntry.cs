using System;

namespace Kudu.Contracts.Editor
{
    /// <summary>
    /// Represents a directory structure. Used by <see cref="VfsControllerBase"/> to browse
    /// a Kudu file system or the git repository.
    /// </summary>
    public class VfsStatEntry
    {
        public string Name { get; set; }

        public long Size { get; set; }

        public DateTimeOffset MTime { get; set; }

        public string Mime { get; set; }

        public string Href { get; set; }
    }
}

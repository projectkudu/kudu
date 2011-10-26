using System;
using System.Collections.Generic;

namespace Kudu.Core.SourceControl
{
    public class ChangeSetDetail
    {
        public ChangeSetDetail()
            : this(null)
        {
        }

        public ChangeSetDetail(ChangeSet changeSet)
        {
            ChangeSet = changeSet;
            Files = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        }

        public int FilesChanged { get; set; }
        public int Deletions { get; set; }
        public int Insertions { get; set; }

        public ChangeSet ChangeSet { get; set; }
        public IDictionary<string, FileInfo> Files { get; private set; }
    }
}

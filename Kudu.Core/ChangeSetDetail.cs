using System;
using System.Collections.Generic;

namespace Kudu.Core {
    public class ChangeSetDetail {
        public ChangeSetDetail()
            : this(null) {
        }

        public ChangeSetDetail(ChangeSet changeSet) {
            ChangeSet = changeSet;
            FileStats = new Dictionary<string, FileInfo>(StringComparer.OrdinalIgnoreCase);
        }

        public int FilesChanged { get; set; }
        public int Deletions { get; set; }
        public int Insertions { get; set; }

        public ChangeSet ChangeSet { get; private set; }
        public IDictionary<string, FileInfo> FileStats { get; private set; }
    }
}

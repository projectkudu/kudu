using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Core {
    public class ChangeSetDetail {
        public ChangeSetDetail(ChangeSet changeSet) {
            ChangeSet = changeSet;
            FileStats = new Dictionary<string, FileStats>(StringComparer.OrdinalIgnoreCase);
            Diffs = new List<FileDiff>();
        }

        public int FilesChanged { get; set; }
        public int Deletions { get; set; }
        public int Insertions { get; set; }

        public ChangeSet ChangeSet { get; private set; }
        public IDictionary<string, FileStats> FileStats { get; private set; }
        public IList<FileDiff> Diffs { get; private set; }
    }
}

using System.Collections.Generic;
using Kudu.Core.SourceControl;

namespace Kudu.Client.Model {
    public class ChangeSetDetailViewModel {
        public ChangeSetDetailViewModel(ChangeSetDetail detail) {
            if (detail.ChangeSet != null) {
                ChangeSet = new ChangeSetViewModel(detail.ChangeSet);
            }
            Deletions = detail.Deletions;
            FilesChanged = detail.FilesChanged;
            Insertions = detail.Insertions;
            Files = detail.Files;
        }

        public ChangeSetViewModel ChangeSet { get; set; }
        public int Deletions { get; set; }
        public int FilesChanged { get; set; }
        public int Insertions { get; set; }
        public IDictionary<string, FileInfo> Files { get; set; }
    }

}
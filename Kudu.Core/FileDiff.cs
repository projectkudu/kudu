using System.Collections.Generic;

namespace Kudu.Core {
    public class FileDiff {
        public FileDiff(string fileName) {
            FileName = fileName;
            Lines = new List<LineDiff>();
        }
        public bool Binary { get; set; }
        public string FileName { get; private set; }
        public IList<LineDiff> Lines { get; private set; }
    }
}

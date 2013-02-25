using System;
using System.Collections.Generic;

namespace Kudu.Core.SourceControl
{
    public class FileInfo
    {
        public FileInfo()
        {
            DiffLines = new List<LineDiff>();
        }

        public int Deletions { get; set; }
        public int Insertions { get; set; }
        public bool Binary { get; set; }
        public ChangeType Status { get; set; }
        public IList<LineDiff> DiffLines { get; private set; }

        public override string ToString()
        {
            return String.Format("{0} {1} (+), {2} (-)", Status, Insertions, Deletions);
        }
    }
}

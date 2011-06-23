using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Core {
    public class FileStats {
        public int Deletions { get; set; }
        public int Insertions { get; set; }

        public override string ToString() {
            return String.Format("{0} (+), {1} (-)", Insertions, Deletions);
        }
    }
}

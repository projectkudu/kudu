using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Core {
    public class FileStatus {
        public FileStatus(string path, ChangeType status) {
            Path = path;
            Status = status;
        }

        public string Path { get; private set; }
        public ChangeType Status { get; private set; }
    }
}

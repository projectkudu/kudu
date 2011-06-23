using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Core {
    public class LineDiff {
        public LineDiff(ChangeType type, string value) {
            Type = type;
            Value = value;
        }
        public ChangeType Type { get; set; }
        public string Value { get; set; }
    }
}

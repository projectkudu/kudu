using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kudu.Core {
    public enum ChangeType {
        None,
        Added,
        Deleted,
        Modified,
        Untracked,
        Renamed
    }
}

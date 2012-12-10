using System;

namespace Kudu.Core.SourceControl
{
    public class HgBranch: Branch
    {
        public HgBranch(string id, string name, bool active) 
            : base(id, name, active) { }

        public override bool IsMaster
        {
            get { return String.Equals(Name, "default", StringComparison.OrdinalIgnoreCase); }
        }
    }
}

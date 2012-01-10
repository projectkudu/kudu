namespace Kudu.Core.SourceControl.Hg
{
    public class HgBranch: Branch
    {
        public HgBranch(string id, string name, bool active) : base(id, name, active) { }

        public override bool IsMaster
        {
            get { return Name == "default"; }
        }
    }
}

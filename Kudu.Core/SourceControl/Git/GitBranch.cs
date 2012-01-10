namespace Kudu.Core.SourceControl.Git
{
    public class GitBranch: Branch
    {
        public GitBranch(string id, string name, bool active) : base(id, name, active) { }

        public override bool IsMaster
        {
            get { return Name == "master"; }
        }
    }
}

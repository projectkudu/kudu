namespace Kudu.Core.SourceControl
{
    public enum RepositoryType
    {
        None,
        Git,
        Mercurial,
        Zip // No git/hg functionality; uses temp local folder; builds with BasicBuilder by default
    }
}

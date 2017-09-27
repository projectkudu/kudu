namespace Kudu.Core.SourceControl
{
    public enum RepositoryType
    {
        None,
        Git,
        Mercurial,
        Zip // Uses separate temporary folder as repo folder; does not conflict with other deployment types
    }
}

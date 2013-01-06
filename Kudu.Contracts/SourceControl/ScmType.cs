
namespace Kudu.Contracts.SourceControl
{
    public enum ScmType 
    { 
        Null, 
        None, 
        Tfs, 
        LocalGit, 
        GitHub, 
        CodePlexGit, 
        CodePlexHg, 
        BitbucketGit, 
        BitbucketHg, 
        Dropbox 
    }
}

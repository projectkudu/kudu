
namespace Kudu.Core.SourceControl
{
    public interface IServerRepository
    {
        bool Exists { get; }
        string CurrentId { get; }
        bool Initialize();
        ChangeSet Initialize(string path);
        RepositoryType GetRepositoryType();
        void Clean();
        void FetchWithoutConflict(string remoteUrl, string remoteAlias, string branchName);
        ChangeSet Commit(string message, string authorName);
        void Update(string id);
    }
}

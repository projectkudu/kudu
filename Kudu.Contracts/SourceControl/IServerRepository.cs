using Kudu.Contracts.SourceControl;

namespace Kudu.Core.SourceControl
{
    public interface IServerRepository
    {
        bool Exists { get; }
        string CurrentId { get; }
        bool Initialize(RepositoryConfiguration configuration);
        ChangeSet Initialize(RepositoryConfiguration configuration, string path);
        RepositoryType GetRepositoryType();
        void Clean();
        void FetchWithoutConflict(string remoteUrl, string remoteAlias, string branchName);
        void SetReceiveInfo(string oldRef, string newRef, string branchName);
        ChangeSet Commit(string message, string authorName);
        void Update(string id);
    }
}

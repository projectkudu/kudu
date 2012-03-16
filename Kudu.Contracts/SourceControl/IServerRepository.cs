using Kudu.Contracts.SourceControl;

namespace Kudu.Core.SourceControl
{
    public interface IServerRepository
    {
        string CurrentId { get; }
        bool Exists { get; }
        PushInfo GetPushInfo();
        bool Initialize(RepositoryConfiguration configuration);
        bool Initialize(RepositoryConfiguration configuration, string path);
        ChangeSet GetChangeSet(string id);
        void Update(string id);
        void Update();
        void Clean();
        RepositoryType GetRepositoryType();
    }
}

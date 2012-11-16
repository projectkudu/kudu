using Kudu.Core.SourceControl;

namespace Kudu.Core.SourceControl
{
    public interface IDeploymentRepository
    {
        void Clean();
        ChangeSet GetChangeSet(string id);
        ReceiveInfo GetReceiveInfo();
        void Update();
        void Update(string id);
        void UpdateSubmodules();
    }
}

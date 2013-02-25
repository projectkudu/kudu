using System;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentStatusManager
    {
        IDeploymentStatusFile Create(string id);
        IDeploymentStatusFile Open(string id);

        DateTime LastModifiedTime { get; }
    }
}

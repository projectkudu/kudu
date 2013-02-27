using System;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentStatusManager
    {
        IDeploymentStatusFile Create(string id);
        IDeploymentStatusFile Open(string id);

        string ActiveDeploymentId { get; set; }

        DateTime LastModifiedTime { get; }
    }
}

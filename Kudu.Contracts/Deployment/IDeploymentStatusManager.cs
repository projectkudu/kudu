using System;
using Kudu.Contracts.Infrastructure;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentStatusManager
    {
        IDeploymentStatusFile Create(string id);
        IDeploymentStatusFile Open(string id);
        void Delete(string id);

        IOperationLock Lock { get; }

        string ActiveDeploymentId { get; set; }

        DateTime LastModifiedTime { get; }
    }
}

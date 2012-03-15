using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManifestWriter : IDeploymentManifestReader
    {
        void AddPaths(IEnumerable<string> paths);
    }
}

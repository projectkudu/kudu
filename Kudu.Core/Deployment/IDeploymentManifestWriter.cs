using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManifestWriter
    {
        void AddPaths(IEnumerable<string> files);
    }
}

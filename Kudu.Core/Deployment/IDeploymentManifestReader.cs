using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManifestReader
    {
        IEnumerable<string> GetPaths();
    }
}

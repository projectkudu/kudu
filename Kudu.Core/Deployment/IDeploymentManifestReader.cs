using System.Collections.Generic;

namespace Kudu.Core.Deployment
{
    public interface IDeploymentManifestReader
    {
        IEnumerable<string> GetPaths();

        // Although this may not be the best place, we are soon going to move the entire
        // Smart copy logic out of the service (and use a node.js module to do this).
        string ManifestFilePath { get; }
    }
}

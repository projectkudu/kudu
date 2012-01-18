using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Kudu.Core.Deployment
{
    public class DeploymentManifest : IDeploymentManifestWriter, IDeploymentManifestReader
    {
        private readonly string _path;

        public DeploymentManifest(string path)
        {
            _path = path;
        }

        public IEnumerable<string> GetFiles()
        {
            if (!File.Exists(_path))
            {
                return Enumerable.Empty<string>();
            }

            return File.ReadAllLines(_path);
        }

        public void AddPaths(IEnumerable<string> files)
        {
            File.WriteAllLines(_path, files);
        }
    }
}

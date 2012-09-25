using System.Collections.Generic;
using System.IO;
using Kudu.Core.Deployment;

namespace Kudu.Services
{
    public class BuildPropertyProvider : IBuildPropertyProvider
    {
        private readonly string _extensionsPath;

        public BuildPropertyProvider(string extensionsPath)
        {
            _extensionsPath = extensionsPath;
        }

        public IDictionary<string, string> GetProperties()
        {
            return new Dictionary<string, string> 
            {
                { "MSBuildExtensionsPath32", _extensionsPath },
                { "KuduReferenceAssemblyDirectory", Path.Combine(_extensionsPath, "ReferenceAssemblies") }
            };
        }
    }
}

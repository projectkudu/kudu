using System.Collections.Generic;
using System.IO;
using System.Web;
using Kudu.Core.Deployment;

namespace Kudu.Services.Web.Services
{
    public class BuildPropertyProvider : IBuildPropertyProvider
    {
        public IDictionary<string, string> GetProperties()
        {
            return new Dictionary<string, string> {
                { "MSBuildExtensionsPath32", Path.Combine(HttpRuntime.AppDomainAppPath, "msbuild") }
            };
        }
    }
}
using System.IO;
using System.Web;
using Kudu.Core;
using Kudu.Core.Deployment;

namespace Kudu.Services.Web.Services
{
    public class DeploymentEnvrionment : IDeploymentEnvironment
    {
        private readonly IEnvironment _environment;

        public DeploymentEnvrionment(IEnvironment environment)
        {
            _environment = environment;
        }

        public string ExePath
        {
            get
            {
                return Path.Combine(HttpRuntime.AppDomainAppPath, @"bin", "kudu.exe");
            }
        }

        public string ApplicationPath
        {
            get
            {
                return _environment.SiteRootPath;
            }
        }

        public string MSBuildExtensionsPath
        {
            get
            {
                return Path.Combine(HttpRuntime.AppDomainAppPath, "msbuild");
            }
        }
    }
}
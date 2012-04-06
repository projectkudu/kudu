using System;
using System.IO;
using System.Web;
using Kudu.Core;
using Kudu.Core.Deployment;

namespace Kudu.Services.Web.Services
{
    public class DeploymentCommandGenerator : IDeploymentCommandGenerator
    {
        private readonly IEnvironment _environment;

        public DeploymentCommandGenerator(IEnvironment environment)
        {
            _environment = environment;
        }

        public string DeploymentEnvironmentVariable
        {
            get
            {
                return "KUDU_EXE";
            }
        }

        public string GetDeploymentExePath()
        {
            return Path.Combine(HttpRuntime.AppDomainAppPath, @"bin", "kudu.exe");
        }

        public string GetDeploymentCommand()
        {
            return String.Format(@"${0} ""{1}"" ""{2}""",
                                 DeploymentEnvironmentVariable,
                                 _environment.ApplicationRootPath,
                                 Path.Combine(HttpRuntime.AppDomainAppPath, "msbuild"));
        }
    }
}
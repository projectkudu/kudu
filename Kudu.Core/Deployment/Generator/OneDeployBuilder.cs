using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    //This is the site builder used for OneDeploy scenarios
    public class OneDeployBuilder : ISiteBuilder
    {
        private string _repositoryPath;

        public OneDeployBuilder(string repositoryPath)
        {
            _repositoryPath = repositoryPath;
        }

        public string ProjectType
        {
            get { return Constants.OneDeploy; }
        }

        public Task Build(DeploymentContext context)
        {
            context.Logger.Log($"Running build. Project type: {ProjectType}");

            FileSystemHelpers.CopyDirectoryRecursive(_repositoryPath, context.OutputPath);

            return Task.CompletedTask;
        }

        public void PostBuild(DeploymentContext context)
        {
            // no-op
            context.Logger.Log($"Skipping post build. Project type: {ProjectType}");
        }
    }
}

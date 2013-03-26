using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.SourceControl.Git;

namespace Kudu.Core.Deployment.Generator
{
    public class NodeSiteBuilder : BaseBasicBuilder
    {
        public NodeSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--node")
        {
        }

        public override Task Build(DeploymentContext context)
        {
            return base.Build(context);
        }
    }
}

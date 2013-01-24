using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl.Git;
using System;
using System.Globalization;
using System.Threading.Tasks;

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
            try
            {
                return base.Build(context);
            }
            finally
            {
                // Since the node deployment script generates a web.config file (if not already there) on the repository,
                // In order to clean up the repository after ourselves we remove this file (it should already be copied to wwwroot)
                // If it's not part of the repository by using "git clean" command.
                SafeCleanWebConfig(context);
            }
        }

        private void SafeCleanWebConfig(DeploymentContext context)
        {
            try
            {
                var git = new GitExecutable(Environment.RepositoryPath, DeploymentSettings.GetCommandIdleTimeout());
                if (!String.IsNullOrEmpty(HomePath))
                {
                    git.SetHomePath(HomePath);
                }
                var args = String.Format(CultureInfo.InvariantCulture, "clean -f {0}\\web.config", this.ProjectPath);
                git.Execute(args);
            }
            catch (Exception ex)
            {
                context.Logger.Log(ex);
            }
        }
    }
}

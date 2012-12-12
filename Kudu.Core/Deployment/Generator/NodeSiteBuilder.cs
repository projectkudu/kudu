using Kudu.Contracts.Settings;
using System;
using System.Globalization;
using System.IO.Abstractions;

namespace Kudu.Core.Deployment.Generator
{
    public class NodeSiteBuilder : BaseBasicBuilder
    {
        public NodeSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--node")
        {
        }

        protected override void PreBuild(DeploymentContext context)
        {
            base.PreBuild(context);
            SelectNodeVersion(context);
        }

        private void SelectNodeVersion(DeploymentContext context)
        {
            var fileSystem = new FileSystem();

            ILogger innerLogger = context.Logger.Log(Resources.Log_SelectNodeJsVersion);

            try
            {
                string sourcePath = String.IsNullOrEmpty(ProjectPath) ? RepositoryPath : ProjectPath;
                string log = NodeSiteEnabler.SelectNodeVersion(fileSystem, Environment.ScriptPath, sourcePath, context.Tracer);

                innerLogger.Log(log);
            }
            catch (Exception ex)
            {
                innerLogger.Log(ex);

                throw;
            }
        }
    }
}

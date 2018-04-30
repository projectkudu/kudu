using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class PHPSiteBuilder : BaseBasicBuilder
    {
        public PHPSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--php")
        {
        }

        public override string ProjectType
        {
            get { return "PHP"; }
        }
    }
}

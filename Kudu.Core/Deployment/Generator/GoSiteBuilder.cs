using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class GoSiteBuilder : BaseBasicBuilder
    {
        public GoSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--go")
        {
        }

        public override string ProjectType
        {
            get { return "GO"; }
        }
    }
}

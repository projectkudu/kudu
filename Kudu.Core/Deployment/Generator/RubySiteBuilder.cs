using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class RubySiteBuilder : BaseBasicBuilder
    {
        public RubySiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--ruby")
        {
        }

        public override string ProjectType
        {
            get { return "RUBY"; }
        }
    }
}

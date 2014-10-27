using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class PythonSiteBuilder : BaseBasicBuilder
    {
        public PythonSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--python")
        {
        }

        public override string ProjectType
        {
            get { return "Python"; }
        }
    }
}

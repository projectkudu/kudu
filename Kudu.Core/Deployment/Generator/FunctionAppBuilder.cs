using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionAppBuilder : BaseBasicBuilder
    {
        public FunctionAppBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath) 
            :base(environment, settings, propertyProvider, repositoryPath, projectPath, "--functionApp")
        {
        }

        public override string ProjectType
        {
            get
            {
                return "functionApp";
            }
        }
    }
}
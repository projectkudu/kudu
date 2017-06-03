using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionBasicBuilder : BaseBasicBuilder
    {
        public FunctionBasicBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--functionApp")
        {
        }

        public override string ProjectType
        {
            get
            {
                return "BASIC FUNCTIONAPP";
            }
        }
    }
}

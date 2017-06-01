using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionNodeBuilder : BaseBasicBuilder
    {
        public FunctionNodeBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--functionApp")
        {
        }

        public override string ProjectType
        {
            get
            {
                return "functionApp Node";
            }
        }
    }
}

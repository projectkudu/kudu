using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionAppBuilder : MicrosoftSiteBuilder
    {
        public FunctionAppBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--functionApp")
        {
        }

        public override string ProjectType
        {
            get
            {
                return "functionApp Msbuild";
            }
        }
    }
}
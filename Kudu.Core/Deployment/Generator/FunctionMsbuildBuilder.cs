using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionMsbuildBuilder : MicrosoftSiteBuilder
    {
        public FunctionMsbuildBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--functionApp")
        {
        }

        public override string ProjectType
        {
            get
            {
                return "MSBUILD FUNCTIONAPP";
            }
        }
    }
}
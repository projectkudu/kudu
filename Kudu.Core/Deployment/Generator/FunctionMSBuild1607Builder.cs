using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionMSBuild1607Builder : MicrosoftSiteBuilder
    {
        // we want to fail early (before running msbuild) if the deployment environment mismatch with the target framework
        public FunctionMSBuild1607Builder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--functionAppMSBuild1607")
        {
            FunctionAppHelper.ThrowsIfVersionMismatch(projectFilePath);
        }

        public override string ProjectType
        {
            get
            {
                return "MSBUILD16.7.0 FUNCTIONAPP";
            }
        }
    }
}

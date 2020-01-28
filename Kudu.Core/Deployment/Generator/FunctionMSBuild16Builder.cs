using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionMSBuild16Builder : MicrosoftSiteBuilder
    {
        // we want to fail early (before running msbuild) if the deployment environment mismatch with the target framework
        public FunctionMSBuild16Builder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--functionAppMSBuild16")
        {
            FunctionAppHelper.ThrowsIfVersionMismatch(projectFilePath);
        }

        public override string ProjectType
        {
            get
            {
                return "MSBUILD16 FUNCTIONAPP";
            }
        }
    }
}

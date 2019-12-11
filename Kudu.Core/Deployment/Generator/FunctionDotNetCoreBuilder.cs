using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Builds .Net Core 3+ Function apps using dotnet.exe
    /// </summary>
    class FunctionDotNetCoreBuilder : MicrosoftSiteBuilder
    {
        public FunctionDotNetCoreBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath, string targetFramework)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--dotNetCoreFunctionApp", targetFramework)
        {
            FunctionAppHelper.ThrowsIfVersionMismatch(projectFilePath);
        }

        public override string ProjectType
        {
            get
            {
                return ".NET CORE FUNCTIONAPP";
            }
        }
    }
}

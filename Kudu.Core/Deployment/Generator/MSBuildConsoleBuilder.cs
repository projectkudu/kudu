using System;
using System.Text;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Console worker consisting a .net console application project which is built and the artifact executable will run as the worker
    /// </summary>
    public class MSBuildConsoleBuilder : MicrosoftSiteBuilder
    {
        public MSBuildConsoleBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--msbuildConsole")
        {
        }

        public override string ProjectType
        {
            get { return "MSBUILD CONSOLE WORKER"; }
        }
    }
}

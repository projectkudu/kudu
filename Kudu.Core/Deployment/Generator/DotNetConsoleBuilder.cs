using System;
using System.Text;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Console worker consisting a .net console application project which is built and the artifact executable will run as the worker
    /// </summary>
    public class DotNetConsoleBuilder : MicrosoftSiteBuilder
    {
        public DotNetConsoleBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--dotNetConsole")
        {
        }

        public override string ProjectType
        {
            get { return ".NET CONSOLE WORKER"; }
        }
    }
}

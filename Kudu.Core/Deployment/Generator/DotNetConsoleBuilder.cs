using System;
using System.Text;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Console worker consists of a console application project which is built and the executable will run on the worker
    /// Build .Net Core 2.2 or less and .Net Frameworks applications using msbuild
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

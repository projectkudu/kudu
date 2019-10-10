using System;
using System.Text;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Console worker consists of a console application project which is built and the executable will run on the worker
    /// Build .Net Core 3+ applications using dotnet.exe
    /// </summary>
    public class DotNetCoreConsoleBuilder : MicrosoftSiteBuilder
    {
        public DotNetCoreConsoleBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--dotNetCoreConsole")
        {
        }

        public override string ProjectType
        {
            get { return ".NET CORE CONSOLE WORKER"; }
        }
    }
}

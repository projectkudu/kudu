using System;
using System.Text;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class WapBuilder : MicrosoftSiteBuilder
    {

        public WapBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--aspWAP")
        {
        }

        public override string ProjectType
        {
            get { return "ASP.NET WAP"; }
        }
    }
}

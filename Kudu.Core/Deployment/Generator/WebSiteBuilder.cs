using System;
using System.Globalization;
using System.Text;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class WebSiteBuilder : MicrosoftSiteBuilder
    {
        private readonly string _projectPath;
        public WebSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, null, solutionPath, "--aspWebSite")
        {
            _projectPath = CleanPath(projectPath);
        }

        // different from other MSSiteBuilders which does this "--aspWebSite {projectFilePath}",
        // instead it uses "--aspWebSite --sitePath {projectPath}"
        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                var commandArguments = new StringBuilder(commandArgument);

                if (!String.IsNullOrEmpty(solutionPath))
                {
                    commandArguments.AppendFormat(CultureInfo.InvariantCulture, " --solutionFile \"{0}\"", solutionPath);
                }

                if (!String.IsNullOrEmpty(_projectPath))
                {
                    commandArguments.AppendFormat(CultureInfo.InvariantCulture, " --sitePath \"{0}\"", _projectPath);
                }

                return commandArguments.ToString();
            }
        }

        public override string ProjectType
        {
            get { return "ASP.NET WEBSITE"; }
        }
    }
}

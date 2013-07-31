using System;
using System.Globalization;
using System.Text;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public class WebSiteBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _solutionPath;

        public WebSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath;
            _solutionPath = solutionPath;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                var commandArguments = new StringBuilder("--aspWebSite");

                if (!String.IsNullOrEmpty(_solutionPath))
                {
                    commandArguments.AppendFormat(CultureInfo.InvariantCulture, " --solutionFile \"{0}\"", CleanPath(_solutionPath));
                }

                if (!String.IsNullOrEmpty(_projectPath))
                {
                    commandArguments.AppendFormat(CultureInfo.InvariantCulture, " --sitePath \"{0}\"", CleanPath(_projectPath));
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

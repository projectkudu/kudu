using Kudu.Contracts.Settings;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

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
                StringBuilder commandArguments = new StringBuilder("--aspWebSite");

                if (!String.IsNullOrEmpty(_solutionPath))
                {
                    commandArguments.AppendFormat(CultureInfo.InvariantCulture, " --solutionFile \"{0}\"", _solutionPath);
                }

                if (!String.IsNullOrEmpty(_projectPath))
                {
                    commandArguments.AppendFormat(CultureInfo.InvariantCulture, " -p \"{0}\"", _projectPath);
                }

                return commandArguments.ToString();
            }
        }
    }
}

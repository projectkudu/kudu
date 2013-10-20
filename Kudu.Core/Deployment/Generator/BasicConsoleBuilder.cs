using System;
using System.Globalization;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Console worker consisting of files and a command line to run (as the worker)
    /// </summary>
    public class BasicConsoleBuilder : GeneratorSiteBuilder
    {
        private const string Argument = "--basicConsole";

        private readonly string _projectPath;

        public BasicConsoleBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                if (!String.IsNullOrEmpty(_projectPath))
                {
                    return String.Format(CultureInfo.InvariantCulture, "{0} --sitePath \"{1}\"", Argument, _projectPath);
                }

                return Argument;
            }
        }

        public override string ProjectType
        {
            get { return "BASIC CONSOLE WORKER"; }
        }
    }
}
using System;
using System.Globalization;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Console worker consisting of files and a command line to run (as the worker)
    /// </summary>
    public class BasicConsoleBuilder : BaseConsoleBuilder
    {
        private const string Argument = "--basicConsole";

        public BasicConsoleBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath)
            : base(environment, settings, propertyProvider, sourcePath, projectPath)
        {
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                if (!String.IsNullOrEmpty(ProjectPath))
                {
                    return String.Format(CultureInfo.InvariantCulture, "{0} --sitePath \"{1}\"", Argument, ProjectPath);
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
using Kudu.Contracts.Settings;
using System;
using System.Globalization;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class BaseBasicBuilder : GeneratorSiteBuilder
    {
        private readonly string _commandArgument;

        protected BaseBasicBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath, string commandArgument)
            : base(environment, settings, propertyProvider, repositoryPath)
        {
            ProjectPath = CleanPath(projectPath);
            _commandArgument = commandArgument;
        }

        protected string ProjectPath { get; private set; }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                if (!String.IsNullOrEmpty(ProjectPath))
                {
                    return String.Format(CultureInfo.InvariantCulture, "{0} --sitePath \"{1}\"", _commandArgument, ProjectPath);
                }

                return _commandArgument;
            }
        }
    }
}

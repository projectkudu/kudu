using Kudu.Contracts.Settings;
using System;
using System.Globalization;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class BaseBasicBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _commandArgument;

        public BaseBasicBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath, string commandArgument)
            : base(environment, settings, propertyProvider, repositoryPath)
        {
            _projectPath = projectPath;
            _commandArgument = commandArgument;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                if (!String.IsNullOrEmpty(_projectPath))
                {
                    return String.Format(CultureInfo.InvariantCulture, "{0} -p \"{1}\"", _commandArgument, _projectPath);
                }

                return _commandArgument;
            }
        }
    }
}

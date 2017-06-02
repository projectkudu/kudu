using Kudu.Contracts.Settings;
using System;
using System.Text;

namespace Kudu.Core.Deployment.Generator
{

    public abstract class MicrosoftSiteBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectFilePath;
        private readonly string _solutionPath;
        private readonly string _commandArgument;

        protected string solutionPath
        {
            get
            {
                return _solutionPath;
            }
        }

        protected string commandArgument
        {
            get
            {
                return _commandArgument;
            }
        }


        protected MicrosoftSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath, string commandArgument)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectFilePath = CleanPath(projectFilePath);
            _solutionPath = CleanPath(solutionPath);
            _commandArgument = commandArgument;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                var commandArguments = new StringBuilder();
                commandArguments.AppendFormat("{0} \"{1}\"", _commandArgument, _projectFilePath);

                if (!String.IsNullOrEmpty(_solutionPath))
                {
                    commandArguments.AppendFormat(" --solutionFile \"{0}\"", _solutionPath);
                }
                else
                {
                    commandArguments.AppendFormat(" --no-solution");
                }

                return commandArguments.ToString();
            }
        }
    }
}

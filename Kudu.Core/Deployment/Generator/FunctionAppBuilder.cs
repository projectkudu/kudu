using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class FunctionAppBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectFilePath;
        private readonly string _solutionPath;

        public FunctionAppBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectFilePath, string solutionPath = null)
            : base(environment, settings, propertyProvider, repositoryPath)
        {
            _projectFilePath = projectFilePath;
            _solutionPath = solutionPath;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                if (string.IsNullOrEmpty(_solutionPath))
                {
                    return $"--functionApp \"{_projectFilePath}\"";
                }
                else
                {
                    return $"--functionApp \"{_projectFilePath}\" --solutionFile \"{_solutionPath}\"";
                }
            }
        }

        public override string ProjectType
        {
            get
            {
                return "functionApp Msbuild";
            }
        }
    }
}
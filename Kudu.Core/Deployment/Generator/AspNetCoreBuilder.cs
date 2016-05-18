using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl;
using System.IO;

namespace Kudu.Core.Deployment.Generator
{
    class AspNetCoreBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _solutionPath;

        public AspNetCoreBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string solutionPath = null)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath;
            _solutionPath = solutionPath;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {

                if (string.IsNullOrEmpty(_solutionPath))
                {
                    return $"--aspNetCore \"{Path.GetDirectoryName(_projectPath)}\"";
                }
                else
                {
                    return $"--aspNetCore \"{Path.GetDirectoryName(_projectPath)}\" --solutionFile {_solutionPath}";
                }
            }
        }

        public override string ProjectType
        {
            get { return "ASP.NET Core"; }
        }
    }
}

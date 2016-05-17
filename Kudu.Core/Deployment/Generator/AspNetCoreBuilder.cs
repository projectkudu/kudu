using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl;
using System.IO;

namespace Kudu.Core.Deployment.Generator
{
    class AspNetCoreBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectPath;

        public AspNetCoreBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                return $"--aspNetCore \"{Path.GetDirectoryName(_projectPath)}\"";
            }
        }

        public override string ProjectType
        {
            get { return "ASP.NET Core"; }
        }
    }
}

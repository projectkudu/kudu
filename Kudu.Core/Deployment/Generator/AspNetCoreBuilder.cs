using System;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class AspNetCoreBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _solutionPath;
        private readonly string _version;

        public AspNetCoreBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string solutionPath = null)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath; // either xproj, csproj or project.json
            _solutionPath = solutionPath;
            if (_projectPath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                _version = "csproj";
            }
            else if (_projectPath.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
            {
                _version = "xproj";
            }
            else
            {
                _version = "project.json";
            }
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {

                if (string.IsNullOrEmpty(_solutionPath))
                {
                    return $"--aspNetCore \"{_projectPath}\"";
                }
                else
                {
                    return $"--aspNetCore \"{_projectPath}\" --solutionFile \"{_solutionPath}\"";
                }
            }
        }

        public override string ProjectType
        {
            get { return $"ASP.NET Core {_version}"; }
        }
    }
}

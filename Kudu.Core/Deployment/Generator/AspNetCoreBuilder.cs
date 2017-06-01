using System;
using Kudu.Contracts.Settings;
using System.Globalization;

namespace Kudu.Core.Deployment.Generator
{
    class AspNetCoreBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectFilePath;
        private readonly string _solutionPath;
        private readonly string _version;

        public AspNetCoreBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath = null)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectFilePath = projectFilePath; // either xproj, csproj or project.json
            _solutionPath = solutionPath;
            if (_projectFilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                _version = "csproj";
            }
            else if (_projectFilePath.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
            {
                // if it's xproj, throw invalidOperationException
                throw new InvalidOperationException("Building Asp.Net Core .xproj is no longer supported in Azure, please move to .csproj");
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
                    return $"--aspNetCore \"{_projectFilePath}\"";
                }
                else
                {
                    return $"--aspNetCore \"{_projectFilePath}\" --solutionFile \"{_solutionPath}\"";
                }
            }
        }

        public override string ProjectType
        {
            get { return $"ASP.NET Core {_version}"; }
        }
    }
}

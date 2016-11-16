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
                    return $"--aspNetCore \"{_projectPath}\""; 
                    // projectfile is either project.json for preview2, ***.csproj for preview3
                }
                else
                {
                    return $"--aspNetCore \"{_projectPath}\" --solutionFile \"{_solutionPath}\"";
                    // if we have a solution file, projectfile is either ***.xproj for preview2, ***.csproj for preview3
                }
            }
        }

        public override string ProjectType
        {
            get { return "ASP.NET Core"; }
        }
    }
}

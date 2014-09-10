using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class ProjectKBuilder : GeneratorSiteBuilder
    {
        private readonly string _projectPath;

        public ProjectKBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                var commandArguments = new StringBuilder();
                commandArguments.AppendFormat("--aspProjectK \"{0}\"", _projectPath);
                return commandArguments.ToString();
            }
        }

        public override string ProjectType
        {
            get { return "ASP.NET ProjectK"; }
        }
    }
}

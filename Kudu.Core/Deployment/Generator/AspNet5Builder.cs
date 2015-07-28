using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    class AspNet5Builder : GeneratorSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _sourcePath;

        public AspNet5Builder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath;
            _sourcePath = sourcePath;
        }

        protected override string ScriptGeneratorCommandArguments
        {
            get
            {
                var commandArguments = new StringBuilder();
                var aspNetSdk = AspNet5Helper.GetAspNet5Sdk(_sourcePath);
                commandArguments.AppendFormat("--aspNet5 \"{0}\" --aspNet5Version \"{1}\" --aspNet5Runtime \"{2}\" --aspNet5Architecture \"{3}\"",
                    _projectPath,
                    aspNetSdk.Version,
                    aspNetSdk.Runtime,
                    aspNetSdk.Architecture);
                return commandArguments.ToString();
            }
        }

        public override string ProjectType
        {
            get { return "ASP.NET 5"; }
        }
    }
}

using Kudu.Contracts.Settings;
using System;
using System.Globalization;

namespace Kudu.Core.Deployment.Generator
{
    public class BasicBuilder : BaseBasicBuilder
    {
        public BasicBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string projectPath)
            : base(environment, settings, propertyProvider, repositoryPath, projectPath, "--basic")
        {
        }
    }
}

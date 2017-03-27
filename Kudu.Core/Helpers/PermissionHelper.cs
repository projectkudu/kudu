using System.IO;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using System;

namespace Kudu.Core.Helpers
{
    public static class PermissionHelper
    {
        public static void Chmod(string permission, string filePath, IEnvironment environment, IDeploymentSettingsManager deploymentSettingManager, ILogger logger)
        {
            var folder = Path.GetDirectoryName(filePath);
            var exeFactory = new ExternalCommandFactory(environment, deploymentSettingManager, null);
            Executable exe = exeFactory.BuildCommandExecutable("/bin/chmod", folder, deploymentSettingManager.GetCommandIdleTimeout(), logger);
            exe.Execute("{0} \"{1}\"", permission, filePath);
        }
    }
}

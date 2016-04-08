using System.IO;
using Kudu.Contracts.Permissions;
using Kudu.Contracts.Settings;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Permissions
{
    /// <summary>
    /// This handler is meant for Linux enviroment only
    /// </summary>
    public class PermissionHandler : IPermissionHandler
    {
        private IEnvironment _environment;
        private IDeploymentSettingsManager _settings;
        private ILogger _logger;

        public PermissionHandler(IEnvironment env, IDeploymentSettingsManager settings, ILogger logger)
        {
            _environment = env;
            _settings = settings;
            _logger = logger;
        }

        public void Chmod(string permission, string filePath)
        {
            var folder = new DirectoryInfo(filePath).Parent;
            var exeFactory = new ExternalCommandFactory(_environment, _settings, null);
            Executable exe = exeFactory.BuildCommandExecutable("/bin/chmod", folder.FullName, _settings.GetCommandIdleTimeout(), _logger);
            exe.Execute("{0} {1}", permission, filePath);
        }
    }
}

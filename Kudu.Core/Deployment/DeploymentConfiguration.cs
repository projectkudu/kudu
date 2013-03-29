using System;
using System.IO;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class DeploymentConfiguration
    {
        internal const string DeployConfigFile = ".deployment";
        private readonly IniFile _iniFile;
        private readonly string _path;

        public DeploymentConfiguration(IDeploymentSettingsManager settings, string path)
        {
            _path = path;

            string deploymentFile = settings.GetValue(SettingsKeys.DeploymentFile) ?? DeployConfigFile;
            _iniFile = new IniFile(Path.Combine(path, deploymentFile));
        }

        public string ProjectPath
        {
            get
            {
                string projectPath = _iniFile.GetSectionValue("config", "project");
                if (String.IsNullOrEmpty(projectPath))
                {
                    return null;
                }

                return Path.GetFullPath(Path.Combine(_path, projectPath.TrimStart('/', '\\')));
            }
        }

        public string Command
        {
            get
            {
                string command = _iniFile.GetSectionValue("config", "command");
                if (String.IsNullOrEmpty(command))
                {
                    return null;
                }

                return command;
            }
        }
    }
}

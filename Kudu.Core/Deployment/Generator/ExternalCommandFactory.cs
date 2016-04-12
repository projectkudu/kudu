using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kudu.Contracts.Settings;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public class ExternalCommandFactory
    {
        public const string KuduSyncCommand = "kudusync";

        internal const string StarterScriptName = "starter.cmd";

        private IEnvironment _environment;
        private IDeploymentSettingsManager _deploymentSettings;
        private string _repositoryPath;

        public ExternalCommandFactory(IEnvironment environment, IDeploymentSettingsManager settings, string repositoryPath)
        {
            _environment = environment;
            _deploymentSettings = settings;
            _repositoryPath = repositoryPath;
        }

        public Executable BuildExternalCommandExecutable(string workingDirectory, string deploymentTargetPath, ILogger logger)
        {
            string sourcePath = _repositoryPath;
            string targetPath = deploymentTargetPath;

            var exe = BuildCommandExecutable(StarterScriptPath, workingDirectory, _deploymentSettings.GetCommandIdleTimeout(), logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.SourcePath, sourcePath, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.TargetPath, targetPath, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.SelectNodeVersionCommandKey, SelectNodeVersionCommand, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.SelectPythonVersionCommandKey, SelectPythonVersionCommand, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.WebJobsDeployCommandKey, WebJobsDeployCommand, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.GoWebConfigTemplate, GoWebConfigTemplate, logger);
            // this script takes in two param, target foler path and output file path
            // what it does is, it assume immediate sub folder of targer folder are all name with version
            // and this script is trying to get the folder name that with largest version name
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.SelectLatestVersionCommandKey, SelectLatestVersionCommand, logger);

            bool isInPlace = false;
            string project = _deploymentSettings.GetValue(SettingsKeys.Project);
            if (!String.IsNullOrEmpty(project))
            {
                isInPlace = PathUtilityFactory.Instance.PathsEquals(Path.Combine(sourcePath, project), targetPath);
            }
            else
            {
                isInPlace = PathUtilityFactory.Instance.PathsEquals(sourcePath, targetPath);
            }

            if (isInPlace)
            {
                UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.InPlaceDeployment, "1", logger);
            }

            // NuGet.exe 1.8 will require an environment variable to make package restore work
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.NuGetPackageRestoreKey] = "true";

            return exe;
        }

        public Executable BuildCommandExecutable(string commandPath, string workingDirectory, TimeSpan idleTimeout, ILogger logger)
        {
            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = new Executable(commandPath, workingDirectory, idleTimeout);
            exe.AddDeploymentSettingsAsEnvironmentVariables(_deploymentSettings);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.WebRootPath, _environment.WebRootPath, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.MSBuildPath, PathUtilityFactory.Instance.ResolveMSBuildPath(), logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.KuduSyncCommandKey, KuduSyncCommand, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.NuGetExeCommandKey, NuGetExeCommand, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.NpmJsPathKey, PathUtilityFactory.Instance.ResolveNpmJsPath(), logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.DnvmPath, DnvmPath, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.GypMsvsVersion, Constants.VCVersion, logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.VCTargetsPath, PathUtilityFactory.Instance.ResolveVCTargetsPath(), logger);
            UpdateToDefaultIfNotSet(exe, WellKnownEnvironmentVariables.VCInstallDir140, PathUtilityFactory.Instance.ResolveVCInstallDirPath(), logger);

            exe.SetHomePath(_environment);

            // Set the path so we can add more variables
            string path = System.Environment.GetEnvironmentVariable("PATH");
            exe.EnvironmentVariables["PATH"] = path;

            return exe;
        }

        private string NuGetExeCommand
        {
            get
            {
                return Path.Combine(_environment.ScriptPath, "nuget.exe");
            }
        }

        private string SelectNodeVersionCommand
        {
            get
            {
                return "node " + QuotePath(Path.Combine(_environment.ScriptPath, "selectNodeVersion"));
            }
        }

        private string SelectPythonVersionCommand
        {
            get
            {
                return "python " + QuotePath(Path.Combine(_environment.ScriptPath, "select_python_version.py"));
            }
        }

        private static string WebJobsDeployCommand
        {
            get { return "deploy_webjobs.cmd"; }
        }

        private string StarterScriptPath
        {
            get
            {
                if (OSDetecter.IsOnWindows())
                {
                    return Path.Combine(_environment.ScriptPath, StarterScriptName);
                }
                else
                {
                    return "/bin/bash";
                }
            }
        }

        private string DnvmPath
        {
            get
            {
                return Path.Combine(_environment.ScriptPath, "dnvm.ps1");
            }
        }

        private string GoWebConfigTemplate
        {
            get
            {
                return Path.Combine(_environment.ScriptPath, "go.web.config.template");
            }
        }

        private string SelectLatestVersionCommand
        {
            get
            {
                return Path.Combine(_environment.ScriptPath, "selectLatestVersion.ps1");
            }
        }

        private void UpdateToDefaultIfNotSet(Executable exe, string key, string defaultValue, ILogger logger)
        {
            var value = _deploymentSettings.GetValue(key);
            if (string.IsNullOrEmpty(value))
            {
                exe.EnvironmentVariables[key] = defaultValue;
            }
            else
            {
                logger.Log("Using custom deployment setting for {0} custom value is '{1}'.", key, value);
                exe.EnvironmentVariables[key] = value;
            }
        }

        private static string QuotePath(string path)
        {
            return String.Concat('"', path, '"');
        }
    }
}

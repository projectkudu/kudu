using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using System.IO;

namespace Kudu.Core.Deployment.Generator
{
    internal class ExternalCommandFactory
    {
        // TODO: Once CustomBuilder is removed, change all internals back to privates

        internal const string SourcePath = "DEPLOYMENT_SOURCE";
        internal const string TargetPath = "DEPLOYMENT_TARGET";
        internal const string MSBuildPath = "MSBUILD_PATH";
        internal const string KuduSyncCommandKey = "KUDU_SYNC_CMD";
        internal const string SelectNodeVersionCommandKey = "KUDU_SELECT_NODE_VERSION_CMD";
        internal const string NpmJsPathKey = "NPM_JS_PATH";
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

        public Executable BuildExternalCommandExecutable(string workingDirectory, string deploymentTargetPath)
        {
            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = new Executable(StarterScriptPath, workingDirectory, _deploymentSettings.GetCommandIdleTimeout());
            exe.AddDeploymentSettingsAsEnvironmentVariables(_deploymentSettings);
            exe.EnvironmentVariables[SourcePath] = _repositoryPath;
            exe.EnvironmentVariables[TargetPath] = deploymentTargetPath;
            exe.EnvironmentVariables[MSBuildPath] = PathUtility.ResolveMSBuildPath();
            exe.EnvironmentVariables[KuduSyncCommandKey] = KuduSyncCommand;
            exe.EnvironmentVariables[SelectNodeVersionCommandKey] = SelectNodeVersionCommand;
            exe.EnvironmentVariables[NpmJsPathKey] = PathUtility.ResolveNpmJsPath();

            // Disable this for now
            // exe.EnvironmentVariables[NuGetCachePathKey] = Environment.NuGetCachePath;

            // NuGet.exe 1.8 will require an environment variable to make package restore work
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.NuGetPackageRestoreKey] = "true";

            exe.SetHomePath(_environment.SiteRootPath);

            // Set the path so we can add more variables
            exe.EnvironmentVariables["PATH"] = System.Environment.GetEnvironmentVariable("PATH");

            // Add the msbuild path and git path to the %PATH% so more tools are available
            var toolsPaths = new[] {
                Path.GetDirectoryName(PathUtility.ResolveMSBuildPath()),
                Path.GetDirectoryName(PathUtility.ResolveGitPath())
            };

            exe.AddToPath(toolsPaths);

            return exe;
        }

        private string KuduSyncCommand
        {
            get
            {
                return Path.Combine(_environment.ScriptPath, "kudusync");
            }
        }

        private string SelectNodeVersionCommand
        {
            get
            {
                return "node \"" + Path.Combine(_environment.ScriptPath, "selectNodeVersion") + "\"";
            }
        }

        private string StarterScriptPath
        {
            get
            {
                return Path.Combine(_environment.ScriptPath, StarterScriptName);
            }
        }
    }
}

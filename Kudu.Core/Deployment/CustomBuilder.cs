using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using System.IO.Abstractions;

namespace Kudu.Core.Deployment
{
    public class CustomBuilder : ISiteBuilder
    {
        private readonly string _command;
        private readonly string _repositoryPath;
        private readonly string _tempPath;
        private readonly string _homePath;
        private readonly string _scriptPath;
        private readonly IBuildPropertyProvider _propertyProvider;
        private readonly IDeploymentSettingsManager _settings;

        private const string SourcePath = "DEPLOYMENT_SOURCE";
        private const string TargetPath = "DEPLOYMENT_TARGET";
        private const string BuildTempPath = "DEPLOYMENT_TEMP";
        private const string PreviousManifestPath = "PREVIOUS_MANIFEST_PATH";
        private const string NextManifestPath = "NEXT_MANIFEST_PATH";
        private const string MSBuildPath = "MSBUILD_PATH";
        private const string StarterScriptName = "starter.cmd";

        public CustomBuilder(string repositoryPath, string tempPath, string command, IBuildPropertyProvider propertyProvider, string homePath, string scriptPath, IDeploymentSettingsManager settings)
        {
            _repositoryPath = repositoryPath;
            _tempPath = tempPath;
            _command = command;
            _propertyProvider = propertyProvider;
            _homePath = homePath;
            _scriptPath = scriptPath;
            _settings = settings;
        }

        public Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();

            ILogger customLogger = context.Logger.Log("Running custom deployment command...");

            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = new Executable(StarterScriptPath, _repositoryPath, _settings.GetCommandIdleTimeout());
            exe.AddDeploymentSettingsAsEnvironmentVariables(_settings);
            exe.EnvironmentVariables[SourcePath] = _repositoryPath;
            exe.EnvironmentVariables[TargetPath] = context.OutputPath;
            exe.EnvironmentVariables[PreviousManifestPath] = (context.PreviousManifest != null) ? context.PreviousManifest.ManifestFilePath : String.Empty;
            exe.EnvironmentVariables[NextManifestPath] = context.ManifestWriter.ManifestFilePath;
            exe.EnvironmentVariables[MSBuildPath] = PathUtility.ResolveMSBuildPath();
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.NuGetPackageRestoreKey] = "true";

            exe.SetHomePath(_homePath);

            // Create a directory for the script output temporary artifacts
            string buildTempPath = Path.Combine(_tempPath, Guid.NewGuid().ToString());
            FileSystemHelpers.EnsureDirectory(buildTempPath);
            exe.EnvironmentVariables[BuildTempPath] = buildTempPath;

            // Populate the enviornment with the build propeties
            foreach (var property in _propertyProvider.GetProperties())
            {
                exe.EnvironmentVariables[property.Key] = property.Value;
            }

            // Set the path so we can add more variables
            exe.EnvironmentVariables["PATH"] = System.Environment.GetEnvironmentVariable("PATH");

            // Add the msbuild path and git path to the %PATH% so more tools are available
            var toolsPaths = new[] {
                Path.GetDirectoryName(PathUtility.ResolveMSBuildPath()),
                Path.GetDirectoryName(PathUtility.ResolveGitPath())
            };

            exe.AddToPath(toolsPaths);

            try
            {
                exe.ExecuteWithProgressWriter(customLogger, context.Tracer, ExternalCommandBuilder.ShouldFilterOutMsBuildWarnings, _command, String.Empty);

                // If the user deployed a node.js site, run the select node version logic on his site to use the correct node.exe
                SelectNodeVersionIfRequired(context);

                tcs.SetResult(null);
            }
            catch (CommandLineException ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();

                // Add the output stream and the error stream to the log for better
                // debugging
                customLogger.Log(ex.Output, LogEntryType.Error);
                customLogger.Log(ex.Error, LogEntryType.Error);

                tcs.SetException(ex);
            }
            finally
            {
                // Clean the temp folder up
                FileSystemHelpers.DeleteDirectorySafe(buildTempPath);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Update iisnode.yml file to use the specific node engine depandant on packages.json file (node engine setting),
        /// This is only done for node.js sites.
        /// </summary>
        private void SelectNodeVersionIfRequired(DeploymentContext context)
        {
            var fileSystem = new FileSystem();
            var nodeSiteEnabler = new NodeSiteEnabler(
                 fileSystem,
                 repoFolder: context.OutputPath,
                 siteFolder: context.OutputPath,
                 scriptPath: _scriptPath,
                 settings: _settings);

            ILogger innerLogger = null;

            try
            {
                if (nodeSiteEnabler.LooksLikeNode())
                {
                    innerLogger = context.Logger.Log(Resources.Log_SelectNodeJsVersion);
                    string log = nodeSiteEnabler.SelectNodeVersion(context.Tracer);

                    if (!String.IsNullOrEmpty(log))
                    {
                        innerLogger.Log(log);
                    }

                }
            }
            catch (Exception ex)
            {
                if (innerLogger != null)
                {
                    innerLogger.Log(ex);
                }

                throw;
            }
        }

        private string StarterScriptPath
        {
            get
            {
                return Path.Combine(_scriptPath, StarterScriptName);
            }
        }
    }
}

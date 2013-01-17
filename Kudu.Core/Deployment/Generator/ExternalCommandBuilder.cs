using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class ExternalCommandBuilder : ISiteBuilder
    {
        // TODO: Once CustomBuilder is removed, change all internals back to privates

        internal const string SourcePath = "DEPLOYMENT_SOURCE";
        internal const string TargetPath = "DEPLOYMENT_TARGET";
        internal const string BuildTempPath = "DEPLOYMENT_TEMP";
        internal const string ManifestPath = "MANIFEST_PATH";
        internal const string MSBuildPath = "MSBUILD_PATH";
        internal const string PreviousManifestPath = "PREVIOUS_MANIFEST_PATH";
        internal const string NextManifestPath = "NEXT_MANIFEST_PATH";
        internal const string KuduSyncCommandKey = "KUDU_SYNC_COMMAND";
        internal const string SelectNodeVersionCommandKey = "KUDU_SELECT_NODE_VERSION_COMMAND";
        internal const string NpmJsPathKey = "NPM_JS_PATH";
        internal const string StarterScriptName = "starter.cmd";

        public ExternalCommandBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath)
        {
            Environment = environment;

            DeploymentSettings = settings;
            RepositoryPath = repositoryPath;
            PropertyProvider = propertyProvider;
            HomePath = environment.SiteRootPath;
        }

        protected IEnvironment Environment { get; private set; }
        protected IDeploymentSettingsManager DeploymentSettings { get; private set; }
        protected string RepositoryPath { get; private set; }
        protected IBuildPropertyProvider PropertyProvider { get; private set; }
        protected string HomePath { get; private set; }

        public abstract Task Build(DeploymentContext context);

        protected void RunCommand(DeploymentContext context, string command)
        {
            ILogger customLogger = context.Logger.Log("Running deployment command...");
            customLogger.Log("Command: " + command);

            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = new Executable(StarterScriptPath, RepositoryPath, DeploymentSettings.GetCommandIdleTimeout());
            exe.AddDeploymentSettingsAsEnvironmentVariables(DeploymentSettings);
            exe.EnvironmentVariables[SourcePath] = RepositoryPath;
            exe.EnvironmentVariables[TargetPath] = context.OutputPath;
            exe.EnvironmentVariables[MSBuildPath] = PathUtility.ResolveMSBuildPath();
            exe.EnvironmentVariables[PreviousManifestPath] = context.PreviousManifestFilePath ?? String.Empty;
            exe.EnvironmentVariables[NextManifestPath] = context.NextManifestFilePath;
            exe.EnvironmentVariables[KuduSyncCommandKey] = KuduSyncCommand;
            exe.EnvironmentVariables[SelectNodeVersionCommandKey] = SelectNodeVersionCommand;
            exe.EnvironmentVariables[NpmJsPathKey] = PathUtility.ResolveNpmJsPath();

            // Disable this for now
            // exe.EnvironmentVariables[NuGetCachePathKey] = Environment.NuGetCachePath;

            // NuGet.exe 1.8 will require an environment variable to make package restore work
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.NuGetPackageRestoreKey] = "true";

            exe.SetHomePath(HomePath);

            // Create a directory for the script output temporary artifacts
            string buildTempPath = Path.Combine(Environment.TempPath, Guid.NewGuid().ToString());
            FileSystemHelpers.EnsureDirectory(buildTempPath);
            exe.EnvironmentVariables[BuildTempPath] = buildTempPath;

            // Populate the enviornment with the build propeties
            foreach (var property in PropertyProvider.GetProperties())
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
                exe.ExecuteWithProgressWriter(customLogger, context.Tracer, ShouldFilterOutMsBuildWarnings, command, String.Empty);
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

                throw new CommandLineException(ex.Message, ex);
            }
            finally
            {
                // Clean the temp folder up
                CleanBuild(context.Tracer, buildTempPath);
                FileSystemHelpers.DeleteDirectorySafe(buildTempPath);
            }
        }

        private void CleanBuild(ITracer tracer, string buildTempPath)
        {
            using (tracer.Step("Cleaning up temp files"))
            {
                try
                {
                    FileSystemHelpers.DeleteDirectorySafe(buildTempPath);
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                }
            }
        }

        private string KuduSyncCommand
        {
            get
            {
                return Path.Combine(Environment.ScriptPath, "kudusync.cmd");
            }
        }

        private string SelectNodeVersionCommand
        {
            get
            {
                return "node \"" + Path.Combine(Environment.ScriptPath, "selectNodeVersion") + "\"";
            }
        }

        private string StarterScriptPath
        {
            get
            {
                return Path.Combine(Environment.ScriptPath, StarterScriptName);
            }
        }

        // TODO: Remove this filter once we figure out how to run the msbuild command without getting these warnings
        internal static bool ShouldFilterOutMsBuildWarnings(string outputLine)
        {
            return outputLine.Contains("MSB3644:") || outputLine.Contains("MSB3270:");
        }
    }
}

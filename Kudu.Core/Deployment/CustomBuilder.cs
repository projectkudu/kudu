using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class CustomBuilder : ISiteBuilder
    {
        private readonly string _command;
        private readonly string _repositoryPath;
        private readonly string _tempPath;
        private readonly IBuildPropertyProvider _propertyProvider;

        private const string SourcePath = "DEPLOYMENT_SOURCE";
        private const string TargetPath = "DEPLOYMENT_TARGET";
        private const string BuildTempPath = "DEPLOYMENT_TEMP";
        private const string PreviousManifestPath = "PREVIOUS_MANIFEST_PATH";
        private const string CurrentManifestPath = "CURRENT_MANIFEST_PATH";
        private const string MSBuildPath = "MSBUILD_PATH";

        public CustomBuilder(string repositoryPath, string tempPath, string command, IBuildPropertyProvider propertyProvider)
        {
            _repositoryPath = repositoryPath;
            _tempPath = tempPath;
            _command = command;
            _propertyProvider = propertyProvider;
        }

        public Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();

            ILogger customLogger = context.Logger.Log("Running custom deployment command...");

            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = new Executable("cmd", _repositoryPath);
            exe.EnvironmentVariables[SourcePath] = _repositoryPath;
            exe.EnvironmentVariables[TargetPath] = context.OutputPath;
            exe.EnvironmentVariables[PreviousManifestPath] = (context.PreviousMainfest != null) ? context.PreviousMainfest.ManifestFilePath : string.Empty;
            exe.EnvironmentVariables[CurrentManifestPath] = context.ManifestWriter.ManifestFilePath;
            exe.EnvironmentVariables[MSBuildPath] = PathUtility.ResolveMSBuildPath();
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.NuGetPackageRestoreKey] = "true";

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
                string output = exe.ExecuteWithConsoleOutput(context.Tracer, "/c " + _command, String.Empty).Item1;

                customLogger.Log(output);

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
    }
}

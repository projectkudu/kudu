using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class ExternalCommandBuilder : ISiteBuilder
    {
        private const string SourcePath = "DEPLOYMENT_SOURCE";
        private const string TargetPath = "DEPLOYMENT_TARGET";
        private const string BuildTempPath = "DEPLOYMENT_TEMP";
        private const string ManifestPath = "MANIFEST_PATH";
        private const string MSBuildPath = "MSBUILD_PATH";
        private const string PreviousManifestPath = "PREVIOUS_MANIFEST_PATH";
        private const string NextManifestPath = "NEXT_MANIFEST_PATH";

        public ExternalCommandBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath)
        {
            Environment = environment;

            // TODO: add all settings as environment variables
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

        // TODO: add specific support in script for this:
        //protected string GetMSBuildExtraArguments()
        //{
            //if (_settings == null) return String.Empty;

            //return _settings.GetValue(SettingsKeys.BuildArgs);
        //}

        protected void RunCommand(DeploymentContext context, string command)
        {
            // TODO: Add dots when there is no activity.

            ILogger customLogger = context.Logger.Log("Running deployment command...");
            customLogger.Log("Command: " + command);

            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = new Executable("cmd", RepositoryPath);
            exe.EnvironmentVariables[SourcePath] = RepositoryPath;
            exe.EnvironmentVariables[TargetPath] = context.OutputPath;
            exe.EnvironmentVariables[MSBuildPath] = PathUtility.ResolveMSBuildPath();
            exe.EnvironmentVariables[PreviousManifestPath] = context.PreviousManifestFilePath ?? String.Empty;
            exe.EnvironmentVariables[NextManifestPath] = context.NextManifestFilePath;

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
                using (var writer = new ProgressWriter())
                {
                    writer.Start();

                    // TODO: The line with the MSB3644 warnings since it's not important
                    string log = exe.Execute(context.Tracer,
                                               output =>
                                               {
                                                   if (output.Contains("MSB3644:") || output.Contains("MSB3270:"))
                                                   {
                                                       return false;
                                                   }

                                                   writer.WriteOutLine(output);
                                                   Console.Out.WriteLine(output);
                                                   return true;
                                               },
                                               error =>
                                               {
                                                   writer.WriteErrorLine(error);
                                                   Console.Error.WriteLine(error);
                                                   return true;
                                               },
                                               Console.OutputEncoding,
                                               "/c " + command,
                                               String.Empty).Item1;

                    // TODO: Is this required
                    customLogger.Log(log);
                }
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
    }
}

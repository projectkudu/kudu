using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class ExternalCommandBuilder : ISiteBuilder
    {
        protected ExternalCommandBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath)
        {
            Environment = environment;

            DeploymentSettings = settings;
            RepositoryPath = repositoryPath;
            PropertyProvider = propertyProvider;
            HomePath = environment.SiteRootPath;

            ExternalCommandFactory = new ExternalCommandFactory(environment, settings, repositoryPath);
        }

        protected IEnvironment Environment { get; private set; }

        protected IDeploymentSettingsManager DeploymentSettings { get; private set; }

        protected string RepositoryPath { get; private set; }

        protected IBuildPropertyProvider PropertyProvider { get; private set; }

        protected string HomePath { get; private set; }

        internal ExternalCommandFactory ExternalCommandFactory { get; private set; }

        public abstract Task Build(DeploymentContext context);

        protected void RunCommand(DeploymentContext context, string command)
        {
            ILogger customLogger = context.Logger.Log("Running deployment command...");
            customLogger.Log("Command: " + command);

            // Creates an executable pointing to cmd and the working directory being
            // the repository root
            var exe = ExternalCommandFactory.BuildExternalCommandExecutable(RepositoryPath, context.OutputPath, customLogger);

            exe.EnvironmentVariables[WellKnownEnvironmentVariables.PreviousManifestPath] = context.PreviousManifestFilePath ?? String.Empty;
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.NextManifestPath] = context.NextManifestFilePath;

            // Create a directory for the script output temporary artifacts
            string buildTempPath = Path.Combine(Environment.TempPath, Guid.NewGuid().ToString());
            FileSystemHelpers.EnsureDirectory(buildTempPath);
            exe.EnvironmentVariables[WellKnownEnvironmentVariables.BuildTempPath] = buildTempPath;

            // Populate the environment with the build properties
            foreach (var property in PropertyProvider.GetProperties())
            {
                exe.EnvironmentVariables[property.Key] = property.Value;
            }

            try
            {
                exe.ExecuteWithProgressWriter(customLogger, context.Tracer, command, String.Empty);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();

                throw;
            }
            finally
            {
                // Clean the temp folder up
                CleanBuild(context.Tracer, buildTempPath);
                FileSystemHelpers.DeleteDirectorySafe(buildTempPath);
            }
        }

        private static void CleanBuild(ITracer tracer, string buildTempPath)
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

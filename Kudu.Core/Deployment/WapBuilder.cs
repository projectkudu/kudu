using System;
using System.Globalization;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class WapBuilder : MsBuildSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _tempPath;
        private readonly string _solutionPath;

        public WapBuilder(IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string tempPath, string solutionPath)
            : base(settings, propertyProvider, sourcePath)
        {
            _projectPath = projectPath;
            _tempPath = tempPath;
            _solutionPath = solutionPath;
        }

        public override Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();
            string buildTempPath = Path.Combine(_tempPath, Guid.NewGuid().ToString());

            ILogger buildLogger = context.Logger.Log(Resources.Log_BuildingWebProject, Path.GetFileName(_projectPath));

            try
            {
                using (context.Tracer.Step("Running msbuild on project file"))
                {
                    string log = BuildProject(context.Tracer, buildTempPath);

                    // Log the details of the build
                    buildLogger.Log(log);
                }
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();

                buildLogger.Log(ex);

                tcs.SetException(ex);

                return tcs.Task;
            }

            ILogger copyLogger = context.Logger.Log(Resources.Log_PreparingFiles);

            try
            {
                using (context.Tracer.Step("Copying files to output directory"))
                {
                    // Copy to the output path and use the previous manifest if there
                    DeploymentHelper.CopyWithManifest(buildTempPath, context.OutputPath, context.PreviousManifest);
                }

                using (context.Tracer.Step("Building manifest"))
                {
                    // Generate a manifest from those build artifacts
                    context.ManifestWriter.AddFiles(buildTempPath);
                }

                // Log the copied files from the manifest
                copyLogger.LogFileList(context.ManifestWriter.GetPaths());

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                context.GlobalLogger.Log(ex);

                copyLogger.Log(ex);

                tcs.SetException(ex);
            }
            finally
            {
                // Clean up the build artifacts after copying them
                CleanBuild(context.Tracer, buildTempPath);
            }

            return tcs.Task;
        }

        internal string BuildProject(ITracer tracer, string buildTempPath)
        {
            string command = GetMSBuildArguments(buildTempPath);


            // Build artifacts into a temp path
            return ExecuteMSBuild(tracer, command);
        }

        internal string GetMSBuildArguments(string buildTempPath)
        {
            string command = @"""{0}"" /nologo /verbosity:m /t:Build /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""{1}"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release";
            string properties = GetPropertyString();
            if (!String.IsNullOrEmpty(properties))
            {
                command += " /p:" + properties;
            }

            string solutionDir = null;
            if (!String.IsNullOrEmpty(_solutionPath))
            {
                solutionDir = Path.GetDirectoryName(_solutionPath) + @"\\";
                command += @" /p:SolutionDir=""{2}""";
            }
            command = String.Format(CultureInfo.InvariantCulture, command, _projectPath, buildTempPath, solutionDir);

            string extraArguments = GetMSBuildExtraArguments();
            if (!String.IsNullOrEmpty(extraArguments))
            {
                command += " " + extraArguments;
            }

            return command;
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

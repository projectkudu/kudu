using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class WapBuilder : MsBuildSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _tempPath;
        private readonly string _solutionPath;

        public WapBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string tempPath)
            : this(propertyProvider, sourcePath, projectPath, tempPath, null)
        {
        }

        public WapBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string tempPath, string solutionPath)
            : base(propertyProvider, sourcePath, tempPath)
        {
            _projectPath = projectPath;
            _tempPath = tempPath;
            _solutionPath = solutionPath;
        }

        public override Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();
            var innerLogger = context.Logger.Log(Resources.Log_BuildingWebProject, Path.GetFileName(_projectPath));
            string buildTempPath = Path.Combine(_tempPath, Guid.NewGuid().ToString());

            try
            {
                string log = null;

                using (context.Tracer.Step("Running msbuild on project file"))
                {
                    log = BuildProject(context.Tracer, buildTempPath);
                }

                using (context.Tracer.Step("Copying files to output directory"))
                {
                    // Copy to the output path and use the previous manifest if there
                    DeploymentHelper.CopyWithManifest(buildTempPath, context.OutputPath, context.PreviousMainfest);
                }

                using (context.Tracer.Step("Building manifest"))
                {
                    // Generate a manifest from those build artifacts
                    context.ManifestWriter.AddFiles(buildTempPath);
                }

                // Log the details of the build
                innerLogger.Log(log);

                // Mark this as done
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                innerLogger.Log(Resources.Log_BuildingWebProjectFailed, LogEntryType.Error);
                innerLogger.Log(ex);

                tcs.SetException(ex);

                context.Tracer.TraceError(ex);
            }
            finally
            {
                // Clean up the build artifacts after copying them
                CleanBuild(context.Tracer, buildTempPath);
            }

            return tcs.Task;
        }

        private string BuildProject(ITracer tracer, string buildTempPath)
        {
            string command = @"""{0}"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""{1}"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release;";
            if (String.IsNullOrEmpty(_solutionPath))
            {
                command += "{2}";
                return ExecuteMSBuild(tracer, command, _projectPath, buildTempPath, GetPropertyString());
            }

            string solutionDir = Path.GetDirectoryName(_solutionPath) + @"\\";
            command += @"SolutionDir=""{2}"";{3}";

            // Build artifacts into a temp path
            return ExecuteMSBuild(tracer, command, _projectPath, buildTempPath, solutionDir, GetPropertyString());
        }

        private void CleanBuild(ITracer tracer, string buildTempPath)
        {
            // Don't block the current thread to clean up the build folder since it could take some time
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    FileSystemHelpers.DeleteDirectorySafe(buildTempPath);
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                }
            });
        }
    }
}

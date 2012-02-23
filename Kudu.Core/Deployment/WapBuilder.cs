using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts;

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
            var innerLogger = context.Logger.Log("Building web project {0}.", Path.GetFileName(_projectPath));
            
            try
            {
                string buildTempPath = Path.Combine(_tempPath, "builds", Guid.NewGuid().ToString());
                string log = null;

                using (context.Profiler.Step("Running msbuild on project file"))
                {
                    log = BuildProject(context.Profiler, buildTempPath);
                }

                using (context.Profiler.Step("Copying files to output directory"))
                {
                    // Copy to the output path and use the previous manifest if there
                    DeploymentHelper.CopyWithManifest(buildTempPath, context.OutputPath, context.PreviousMainfest);
                }

                using (context.Profiler.Step("Building manifest"))
                {
                    // Generate a manifest from those build artifacts
                    context.ManifestWriter.AddFiles(buildTempPath);
                }

                innerLogger.Log(log);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                innerLogger.Log("Building web project failed.", LogEntryType.Error);
                innerLogger.Log(ex);

                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        private string BuildProject(IProfiler profiler, string buildTempPath)
        {
            string command = @"""{0}"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""{1}"";AutoParameterizationWebConfigConnectionStrings=false;Configuration=Release;";
            if (String.IsNullOrEmpty(_solutionPath))
            {
                command += "{2}";
                return ExecuteMSBuild(profiler, command, _projectPath, buildTempPath, GetPropertyString());
            }

            string solutionDir = Path.GetDirectoryName(_solutionPath) + @"\\";
            command += @"SolutionDir=""{2}"";{3}";

            // Build artifacts into a temp path
            return ExecuteMSBuild(profiler, command, _projectPath, buildTempPath, solutionDir, GetPropertyString());
        }
    }
}

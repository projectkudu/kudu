using System;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class WapBuilder : SolutionBasedSiteBuilder
    {
        private readonly string _projectPath;
        private readonly string _tempPath;

        public WapBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string solutionPath, string projectPath, string tempPath)
            : base(propertyProvider, sourcePath, solutionPath)
        {
            _projectPath = projectPath;
            _tempPath = tempPath;
        }

        protected override Task BuildProject(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();
            var innerLogger = context.Logger.Log("Building web project {0}.", Path.GetFileName(_projectPath));
            string solutionDir = SolutionDir + @"\\";

            try
            {
                string buildTempPath = Path.Combine(_tempPath, "builds", Guid.NewGuid().ToString());
                string log = null;

                using (context.Profiler.Step("Running msbuild on project file"))
                {
                    // Build artifacts into a temp path                    
                    log = ExecuteMSBuild(innerLogger, @"""{0}"" /nologo /verbosity:m /t:pipelinePreDeployCopyAllFilesToOneFolder /p:_PackageTempDir=""{1}"";AutoParameterizationWebConfigConnectionStrings=false;SolutionDir=""{2}"";{3}", _projectPath, buildTempPath, solutionDir, GetPropertyString());
                }

                using (context.Profiler.Step("Copying files to output directory"))
                {
                    // Copy to the output path and use the previous manifest if there
                    DeploymentHelpers.CopyWithManifest(buildTempPath, context.OutputPath, context.PreviousMainfest);
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
    }
}

using System;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;

namespace Kudu.Core.Deployment
{
    public class WebSiteBuilder : SolutionBasedSiteBuilder
    {
        private readonly string _projectPath;

        public WebSiteBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath, string tempPath, string nugetCachePath, string solutionPath)
            : base(propertyProvider, sourcePath, tempPath, nugetCachePath, solutionPath)
        {
            _projectPath = projectPath;
        }

        protected override Task BuildProject(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();
            ILogger copyLogger = context.Logger.Log(Resources.Log_CopyingFiles);

            try
            {
                using (context.Tracer.Step("Copying files to output directory"))
                {
                    // Copy to the output path
                    DeploymentHelper.CopyWithManifest(_projectPath, context.OutputPath, context.PreviousMainfest);
                }

                using (context.Tracer.Step("Building manifest"))
                {
                    // Generate the manifest from the project path
                    context.ManifestWriter.AddFiles(_projectPath);
                }

                // Log the copied files from the manifest
                copyLogger.LogFileList(context.ManifestWriter.GetPaths());

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                copyLogger.Log(ex);

                tcs.SetException(ex);
            }

            return tcs.Task;
        }
    }
}

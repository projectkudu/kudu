using System;
using System.Threading.Tasks;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class WebSiteBuilder : SolutionBasedSiteBuilder
    {
        private readonly string _projectPath;

        public WebSiteBuilder(IBuildPropertyProvider propertyProvider, string sourcePath, string solutionPath, string projectPath, string tempPath)
            : base(propertyProvider, sourcePath, solutionPath, tempPath)
        {
            _projectPath = projectPath;
        }

        protected override Task BuildProject(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();
            var innerLogger = context.Logger.Log("Using website project {0}.", _projectPath);

            try
            {
                using (context.Profiler.Step("Copying files to output directory"))
                {
                    // Copy to the output path
                    DeploymentHelper.CopyWithManifest(_projectPath, context.OutputPath, context.PreviousMainfest);
                }

                using (context.Profiler.Step("Building manifest"))
                {
                    // Generate the manifest from the project path
                    context.ManifestWriter.AddFiles(_projectPath);
                }

                innerLogger.Log("Done.");
                tcs.SetResult(null);
            }
            catch (Exception e)
            {
                innerLogger.Log("Copying website failed.", LogEntryType.Error);
                innerLogger.Log(e);
                tcs.SetException(e);
            }

            return tcs.Task;
        }
    }
}

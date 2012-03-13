using System;
using System.Threading.Tasks;

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
            var innerLogger = context.Logger.Log(Resources.Log_UsingWebsiteProject, _projectPath);

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

                tcs.SetResult(null);
            }
            catch (Exception e)
            {
                innerLogger.Log(Resources.Log_CopyingWebsiteFailed, LogEntryType.Error);
                innerLogger.Log(e);
                tcs.SetException(e);
            }

            return tcs.Task;
        }
    }
}

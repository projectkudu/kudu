using System;
using System.IO;
using System.Threading.Tasks;
using nji;

namespace Kudu.Core.Deployment
{
    public class BasicBuilder : ISiteBuilder
    {
        private const string NodeModulesPath = "node_modules";

        private readonly string _sourcePath;

        public BasicBuilder(string sourcePath)
        {
            _sourcePath = sourcePath;
        }

        public Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();

            var innerLogger = context.Logger.Log("Copying files.");
            innerLogger.Log("Copying files to {0}.", context.OutputPath);

            try
            {                
                using (context.Profiler.Step("Copying files to output directory"))
                {
                    // Copy to the output path and use the previous manifest if there
                    DeploymentHelpers.CopyWithManifest(_sourcePath, context.OutputPath, context.PreviousMainfest);
                }

                using (context.Profiler.Step("Downloading node packages"))
                {
                    // Download node packages
                    DownloadNodePackages(context);
                }

                using (context.Profiler.Step("Building manifest"))
                {
                    // Generate a manifest from those build artifacts
                    context.ManifestWriter.AddFiles(_sourcePath);
                }

                innerLogger.Log("Done.");
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                innerLogger.Log("Copying files failed.");
                innerLogger.Log(ex);
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Download node packages as part of the deployment
        /// </summary>
        private void DownloadNodePackages(DeploymentContext context)
        {
            var runner = new Program();
            runner.ModulesDir = Path.Combine(context.OutputPath, NodeModulesPath);
            runner.TempDir = Path.Combine(runner.ModulesDir, ".tmp");
            runner.Logger = context.Logger;
            runner.UpdateStatusText = context.ProgressReporter.ReportProgress;

            runner.InstallDependencies(context.OutputPath);
        }
    }
}

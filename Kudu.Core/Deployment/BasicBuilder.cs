using System;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class BasicBuilder : ISiteBuilder
    {
        private const string PackageJsonFile = "package.json";

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
                    DeploymentHelper.CopyWithManifest(_sourcePath, context.OutputPath, context.PreviousMainfest);
                }

                // Download node packages
                DownloadNodePackages(innerLogger, context);

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
        private void DownloadNodePackages(ILogger logger, DeploymentContext context)
        {
            // Check to see if there's a package.json file
            string packagePath = Path.Combine(context.OutputPath, PackageJsonFile);

            if (!File.Exists(packagePath))
            {
                // If the package.json file doesn't exist then don't bother to run npm install
                return;
            }

            using (context.Profiler.Step("Downloading node packages"))
            {
                var npm = new NpmExecutable(context.OutputPath);

                if (!npm.IsAvailable)
                {
                    return;
                }

                // Run install on the output directory
                string log = npm.Execute(context.Profiler, "install").Item1;
                logger.Log(log);
            }
        }
    }
}

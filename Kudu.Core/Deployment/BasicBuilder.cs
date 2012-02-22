using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment
{
    public class BasicBuilder : ISiteBuilder
    {
        private const string PackageJsonFile = "package.json";

        private readonly string _sourcePath;
        private readonly string _tempPath;

        public BasicBuilder(string sourcePath, string tempPath)
        {
            _sourcePath = sourcePath;
            _tempPath = tempPath;
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
                    logger.Log("NPM not installed or couldn't be located. Skipping package installation.");
                    return;
                }

                // Set the npm proxy settings based on the default settings
                var proxy = WebRequest.DefaultWebProxy;
                var httpProxyUrl = proxy.GetProxy(new Uri("http://registry.npmjs.org/"));
                var httpsProxyUrl = proxy.GetProxy(new Uri("https://registry.npmjs.org/"));

                if (httpProxyUrl != null)
                {
                    npm.EnvironmentVariables["HTTP_PROXY"] = httpProxyUrl.ToString();
                }

                if (httpsProxyUrl != null)
                {
                    npm.EnvironmentVariables["HTTPS_PROXY"] = httpsProxyUrl.ToString();
                }

                // Use the temp path as the user profile path in case we don't have the right
                // permission set. This normally happens under IIS as a restricted user (ApplicationPoolIdentity).
                string npmUserProfile = Path.Combine(_tempPath, "npm");
                npm.EnvironmentVariables["USERPROFILE"] = npmUserProfile;
                npm.EnvironmentVariables["LocalAppData"] = npmUserProfile;
                npm.EnvironmentVariables["AppData"] = npmUserProfile;

                try
                {
                    // Use the http proxy since https is failing for some reason
                    npm.Execute("config set registry \"http://registry.npmjs.org/\"");
                }
                catch(Exception ex)
                {
                    // This fails if it's already set
                    Debug.WriteLine(ex.Message);
                }

                // Run install on the output directory
                string log = npm.Execute(context.Profiler, "install").Item1;
                logger.Log(log);
            }
        }
    }
}

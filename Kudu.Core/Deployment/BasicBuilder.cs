using System;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;

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

            var innerLogger = context.Logger.Log(Resources.Log_CopyingFiles);
            innerLogger.Log(Resources.Log_CopyingFilesToDirectory, context.OutputPath);

            try
            {
                using (context.Tracer.Step("Copying files to output directory"))
                {
                    // Copy to the output path and use the previous manifest if there
                    DeploymentHelper.CopyWithManifest(_sourcePath, context.OutputPath, context.PreviousMainfest);
                }

                // Download node packages
                DownloadNodePackages(innerLogger, context);

                using (context.Tracer.Step("Building manifest"))
                {
                    // Generate a manifest from those build artifacts
                    context.ManifestWriter.AddFiles(_sourcePath);
                }

                innerLogger.Log("Done.");
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                innerLogger.Log(Resources.Log_CopyingFilesFailed);
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

            using (context.Tracer.Step("Downloading node packages"))
            {
                var npm = new NpmExecutable(context.OutputPath);


                if (!npm.IsAvailable)
                {
                    logger.Log(Resources.Log_NpmNotInstalled);
                    return;
                }

                // Set the npm proxy settings based on the default settings
                var proxy = WebRequest.DefaultWebProxy;
                var httpUrl = new Uri("http://registry.npmjs.org/");
                var httpsUrl = new Uri("https://registry.npmjs.org/");
                var proxyHttpProxyUrl = proxy.GetProxy(httpUrl);
                var proxyHttpsProxyUrl = proxy.GetProxy(httpsUrl);

                if (proxyHttpProxyUrl != httpUrl)
                {
                    npm.EnvironmentVariables["HTTP_PROXY"] = proxyHttpProxyUrl.ToString();
                }

                if (proxyHttpsProxyUrl != httpsUrl)
                {
                    npm.EnvironmentVariables["HTTPS_PROXY"] = proxyHttpsProxyUrl.ToString();
                }

                try
                {
                    // Use the http proxy since https is failing for some reason
                    npm.Execute("config set registry \"http://registry.npmjs.org/\"");
                }
                catch (Exception ex)
                {
                    // This fails if it's already set
                    context.Tracer.Trace(ex.Message);
                }

                // Run install on the output directory
                string log = npm.Execute(context.Tracer, "install").Item1;
                logger.Log(log);
            }
        }
    }
}

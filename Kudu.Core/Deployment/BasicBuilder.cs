using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class BasicBuilder : ISiteBuilder
    {
        private const string PackageJsonFile = "package.json";
        private const string NodeDetectionFile = "server.js";
        private const string WebConfigFile = "web.config";

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

            ILogger innerLogger = context.Logger.Log(Resources.Log_PreparingFiles);

            try
            {
                using (context.Tracer.Step("Copying files to output directory"))
                {
                    // Copy to the output path and use the previous manifest if there
                    DeploymentHelper.CopyWithManifest(_sourcePath, context.OutputPath, context.PreviousMainfest);
                }

                using (context.Tracer.Step("Building manifest"))
                {
                    // Generate a manifest from those build artifacts
                    context.ManifestWriter.AddFiles(_sourcePath);
                }

                // Log the copied files from the manifest
                innerLogger.LogFileList(context.ManifestWriter.GetPaths());
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                context.GlobalLogger.Log(ex);

                innerLogger.Log(ex);

                tcs.SetException(ex);

                // Bail out early
                return tcs.Task;
            }

            try
            {
                // Download node packages
                DownloadNodePackages(context);

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' when downloding
                // npm packages has already been captured.
                context.GlobalLogger.LogError();

                tcs.SetException(ex);
            }

            AddIISNodeConfig(context);

            return tcs.Task;
        }

        /// <summary>
        /// Download node packages as part of the deployment.
        /// </summary>
        private void DownloadNodePackages(DeploymentContext context)
        {
            // Check to see if there's a package.json file
            string packagePath = Path.Combine(context.OutputPath, PackageJsonFile);

            if (!File.Exists(packagePath))
            {
                // If the package.json file doesn't exist then don't bother to run npm install
                return;
            }

            ILogger innerLogger = context.Logger.Log(Resources.Log_DownloadingNodePackages);

            using (context.Tracer.Step("Downloading node packages"))
            {
                var npm = new NpmExecutable(context.OutputPath);

                if (!npm.IsAvailable)
                {
                    context.Tracer.TraceError(Resources.Log_NpmNotInstalled);

                    innerLogger.Log(Resources.Log_NpmNotInstalled, LogEntryType.Error);
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

                // REVIEW: Do we still need this?
                try
                {
                    // Use the http proxy since https is failing for some reason
                    npm.Execute("config set registry \"http://registry.npmjs.org/\"");
                }
                catch (Exception ex)
                {
                    // This fails if it's already set
                    context.Tracer.TraceError(ex);
                }

                try
                {
                    // Run install on the output directory
                    string log = npm.ExecuteWithConsoleOutput(context.Tracer, "install").Item1;

                    if (String.IsNullOrWhiteSpace(log))
                    {
                        innerLogger.Log(Resources.Log_PackagesAlreadyInstalled);
                    }
                    else
                    {
                        innerLogger.Log(log);
                    }
                }
                catch (Exception ex)
                {
                    // Log the exception
                    innerLogger.Log(ex);

                    // re-throw
                    throw;
                }
            }
        }

        /// <summary>
        /// Add a web.config file if we detect a Node site
        /// </summary>
        private void AddIISNodeConfig(DeploymentContext context)
        {
            // Check if this seems to be a Node app
            string serverJs = Path.Combine(context.OutputPath, NodeDetectionFile);
            if (File.Exists(serverJs))
            {
                // If there is no web.config file already, create one for iinode 
                string webConfig = Path.Combine(context.OutputPath, WebConfigFile);
                if (!File.Exists(webConfig))
                {
                    context.Logger.Log(Resources.Log_CreatingNodeConfig);
                    File.WriteAllText(webConfig, Resources.IisNodeWebConfig);
                }
            }
        }
    }
}

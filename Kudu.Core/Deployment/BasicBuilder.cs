using System;
using System.IO;
using System.Linq;
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
        private readonly string[] NodeDetectionFiles = new[] { "server.js", "app.js" };
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

                AddIISNodeConfig(context);

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

            ILogger innerLogger = context.Logger.Log(Resources.Log_RunningNPM);

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
            // If the repo already has a config file, don't do anything
            if (File.Exists(Path.Combine(_sourcePath, WebConfigFile)))
            {
                return;
            }

            foreach (var nodeDetectionFile in NodeDetectionFiles)
            {
                // If the node detection file exists, create an iisnode web.config file for it
                string fullPath = Path.Combine(context.OutputPath, nodeDetectionFile);
                if (File.Exists(fullPath))
                {
                    using (context.Tracer.Step(Resources.Log_CreatingNodeConfig))
                    {
                        context.Logger.Log(Resources.Log_CreatingNodeConfig);
                        File.WriteAllText(
                            Path.Combine(context.OutputPath, WebConfigFile),
                            String.Format(Resources.IisNodeWebConfig, nodeDetectionFile));
                        return;
                    }
                }
            }

            // If we couldn't treat it as a Node site, but it appears that the user expects it to be,
            // give a warning.
            if (LooksLikeNodeSite(context.OutputPath))
            {
                context.Logger.Log(Resources.Log_NodeWithMissingServerJs);
            }
        }

        private bool LooksLikeNodeSite(string webRoot)
        {
            // If it has a node_modules folder, it's likely Node
            if (Directory.Exists(Path.Combine(webRoot, "node_modules")))
            {
                return true;
            }

            // If it has any PHP/HTML files at the root, treat it as non-Node
            if (Directory.EnumerateFiles(webRoot, "*.php").Any() || Directory.EnumerateFiles(webRoot, "*.htm").Any() || Directory.EnumerateFiles(webRoot, "*.html").Any())
            {
                return false;
            }

            // Treat it as Node if it has at least one .js file at the root
            return Directory.EnumerateFiles(webRoot, "*.js").Any();
        }
    }
}

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    public class BasicBuilder : ISiteBuilder
    {
        private const string PackageJsonFile = "package.json";

        private readonly string _sourcePath;
        private readonly string _tempPath;
        private readonly string _scriptPath;
        private readonly string _homePath;

        public BasicBuilder(string sourcePath, string tempPath, string scriptPath, string homePath)
        {
            _sourcePath = sourcePath;
            _tempPath = tempPath;
            _scriptPath = scriptPath;
            _homePath = homePath;
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

                SelectNodeVersion(context);

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

                npm.SetHomePath(_homePath);

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
                    string log = null;

                    using (var writer = new ProgressWriter())
                    {
                        writer.Start();

                        // Run install on the output directory
                        log = npm.Install(context.Tracer, writer);
                    }

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
            var nodeSiteEnabler = new NodeSiteEnabler(
                new FileSystem(),
                repoFolder: _sourcePath,
                siteFolder: context.OutputPath,
                scriptPath: _scriptPath);

            // Check if need to do anythng related to Node
            if (nodeSiteEnabler.NeedNodeHandling())
            {
                // If we can figure out the start file, create the config file.
                // Otherwise give a warning
                string nodeStartFile = nodeSiteEnabler.GetNodeStartFile();
                if (nodeStartFile != null)
                {
                    context.Logger.Log(Resources.Log_CreatingNodeConfig);
                    nodeSiteEnabler.CreateConfigFile(nodeStartFile);
                }
                else
                {
                    context.Logger.Log(Resources.Log_NodeWithMissingServerJs);
                }
            }
        }

        /// <summary>
        /// Selects a node.js version to run the application with and augments iisnode.yml accordingly
        /// </summary>
        private void SelectNodeVersion(DeploymentContext context)
        {
            var fileSystem = new FileSystem();
            var nodeSiteEnabler = new NodeSiteEnabler(
                 fileSystem,
                 repoFolder: _sourcePath,
                 siteFolder: context.OutputPath,
                 scriptPath: _scriptPath);

            ILogger innerLogger = null;

            try
            {
                if (nodeSiteEnabler.LooksLikeNode())
                {
                    innerLogger = context.Logger.Log(Resources.Log_SelectNodeJsVersion);
                    string log = nodeSiteEnabler.SelectNodeVersion(context.Tracer);

                    if (!String.IsNullOrEmpty(log))
                    {
                        innerLogger.Log(log);
                    }

                }
            }
            catch (Exception ex)
            {
                if (innerLogger != null)
                {
                    innerLogger.Log(ex);
                }

                throw;
            }
        }
    }
}

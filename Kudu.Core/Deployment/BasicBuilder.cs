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
                    string log = null;
                    using (var writer = new ProgressWriter())
                    {
                        writer.Start();
                        // Run install on the output directory
                        log = npm.Execute(context.Tracer, "install").Item1;
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
                catch(Exception ex)
                {
                    // Log the exception
                    innerLogger.Log(ex);

                    // re-throw
                    throw;
                }
            }
        }
    }
}

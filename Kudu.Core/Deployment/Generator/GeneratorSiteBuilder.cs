using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Helpers;
using Kudu.Core.Tracing;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class GeneratorSiteBuilder : ExternalCommandBuilder
    {
        private const string ScriptGeneratorCommandArgumentsFormat = "-y --no-dot-deployment -r \"{0}\" -o \"{1}\" {2}";
        private const string DeploymentCommandCacheKeyFileName = "deploymentCacheKey";

        private static readonly string KuduVersion = typeof(GeneratorSiteBuilder).Assembly.GetName().Version.ToString();

        protected GeneratorSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath)
            : base(environment, settings, propertyProvider, repositoryPath)
        {
        }

        // TODO: Do we still need this
        /*protected string GetPropertyString()
        {
            return String.Join(";", _propertyProvider.GetProperties().Select(p => String.Format("{0}=\"{1}\"", p.Key, p.Value)));
        }*/

        public override Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();

            // TODO: Localization for the default script
            ILogger buildLogger = context.Logger.Log(Resources.Log_GeneratingDeploymentScript, Path.GetFileName(RepositoryPath));

            try
            {
                GenerateScript(context, buildLogger);
                string deploymentScriptPath = String.Format("\"{0}\"", DeploymentManager.GetCachedDeploymentScriptPath(Environment));
                RunCommand(context, deploymentScriptPath, context.IgnoreManifest);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        protected abstract string ScriptGeneratorCommandArguments { get; }

        /// <summary>
        /// Due to node issue, an argument cannot end with \ followed by a " (\"),
        /// This causes the arguments to be parsed incorrectly.
        /// </summary>
        protected static string CleanPath(string path)
        {
            return path != null ? path.TrimEnd('\\') : null;
        }

        private void GenerateScript(DeploymentContext context, ILogger buildLogger)
        {
            try
            {
                using (context.Tracer.Step("Generating deployment script"))
                {
                    var scriptGenerator = ExternalCommandFactory.BuildExternalCommandExecutable(RepositoryPath, context.OutputPath, buildLogger);

                    // Set home path to the user profile so cache directories created by azure-cli are created there
                    scriptGenerator.SetHomePath(System.Environment.GetEnvironmentVariable("APPDATA"));

                    var scriptGeneratorCommandArguments = String.Format(ScriptGeneratorCommandArgumentsFormat, RepositoryPath, Environment.DeploymentToolsPath, ScriptGeneratorCommandArguments);
                    var scriptGeneratorCommand = "\"{0}\" {1}".FormatInvariant(DeploymentScriptGeneratorToolPath, scriptGeneratorCommandArguments);

                    bool cacheUsed = UseCachedDeploymentScript(scriptGeneratorCommandArguments, context);
                    if (!cacheUsed)
                    {
                        buildLogger.Log(Resources.Log_DeploymentScriptGeneratorCommand, scriptGeneratorCommandArguments);

                        scriptGenerator.ExecuteWithProgressWriter(buildLogger, context.Tracer, scriptGeneratorCommand);

                        if (!OSDetector.IsOnWindows())
                        {
                            // Kuduscript output is typically not given execute permission, so add it
                            var deploymentScriptPath = DeploymentManager.GetCachedDeploymentScriptPath(Environment);
                            PermissionHelper.Chmod("ugo+x", deploymentScriptPath, Environment, DeploymentSettings, buildLogger);
                        }

                        CacheDeploymentScript(scriptGeneratorCommandArguments, context);
                    }
                    else
                    {
                        buildLogger.Log(Resources.Log_DeploymentScriptGeneratorUsingCache, scriptGeneratorCommandArguments);
                    }
                }
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();

                throw;
            }
        }

        private void CacheDeploymentScript(string scriptGeneratorCommand, DeploymentContext context)
        {
            try
            {
                string cachedKeyFilePath = Path.Combine(Environment.DeploymentToolsPath, DeploymentCommandCacheKeyFileName);

                // Cache key contains the current kudu version and the command arguments for the deployment script generator
                string[] cacheKeyFileContent = new string[] { KuduVersion, scriptGeneratorCommand };
                File.WriteAllLines(cachedKeyFilePath, cacheKeyFileContent);

                context.Tracer.Trace("Saved cached version of the deployment script for command {0}", scriptGeneratorCommand);
            }
            catch (Exception ex)
            {
                // Do not fail the deployment on failure to save to cache but log the failure
                context.Tracer.Trace("Failed to save cached version of the deployment script for command {0}", scriptGeneratorCommand);
                context.Tracer.TraceError(ex);
            }
        }

        private bool UseCachedDeploymentScript(string scriptGeneratorCommand, DeploymentContext context)
        {
            string cacheKeyFilePath = Path.Combine(Environment.DeploymentToolsPath, DeploymentCommandCacheKeyFileName);
            string deploymentScriptPath = DeploymentManager.GetCachedDeploymentScriptPath(Environment);

            try
            {
                if (!File.Exists(cacheKeyFilePath))
                {
                    return false;
                }

                string[] cacheKeyFileContent = File.ReadAllLines(cacheKeyFilePath).Where(line => !String.IsNullOrEmpty(line)).ToArray();

                // Make sure the cache key file contains exacly 2 lines and the first one is the same as the current running kudu version
                if (cacheKeyFileContent.Length != 2 || cacheKeyFileContent[0] != KuduVersion)
                {
                    return false;
                }

                string cachedDeploymentCommandArguments = cacheKeyFileContent[1];

                // If we use the same deployment script generator command
                if (scriptGeneratorCommand == cachedDeploymentCommandArguments)
                {
                    // And if there is a cached deployment script from previous deployment
                    if (File.Exists(deploymentScriptPath))
                    {
                        context.Tracer.Trace("Using cached version of the deployment script for command: {0}", scriptGeneratorCommand);

                        return true;
                    }
                }

                return false;
            }
            catch (Exception ex)
            {
                // Do not fail the deployment on failure to use cache but log the failure
                context.Tracer.Trace("Failed to use cached version of the deployment script for command: {0}", scriptGeneratorCommand);
                context.Tracer.TraceError(ex);
                return false;
            }
        }

        private string DeploymentScriptGeneratorToolPath
        {
            get
            {
                if (OSDetector.IsOnWindows())
                {
                    return Path.Combine(Environment.NodeModulesPath, ".bin", "kuduscript.cmd");
                }
                else
                {
                    return Path.Combine(Environment.NodeModulesPath, ".bin", "kuduscript");
                }
            }
        }
    }
}

using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class GeneratorSiteBuilder : ExternalCommandBuilder
    {
        private const string ScriptGeneratorCommandFormat = "site deploymentscript -y --no-dot-deployment -r \"{0}\" {1}";
        private const string DeploymentScriptFileName = "deploy.cmd";
        private const string DeploymentCommandArgumentsFileName = "deploymentCommandArguments";

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
                RunCommand(context, DeploymentScriptFileName);
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
                    var scriptGenerator = new Executable(DeploymentScriptGeneratorToolPath, RepositoryPath, DeploymentSettings.GetCommandIdleTimeout());

                    // Set home path to the user profile so cache directories created by azure-cli are created there
                    scriptGenerator.SetHomePath(System.Environment.GetEnvironmentVariable("APPDATA"));

                    var scriptGeneratorCommand = String.Format(ScriptGeneratorCommandFormat, RepositoryPath, ScriptGeneratorCommandArguments);

                    bool cacheUsed = UseCachedDeploymentScript(scriptGeneratorCommand, context);

                    if (!cacheUsed)
                    {
                        buildLogger.Log(Resources.Log_DeploymentScriptGeneratorCommand, scriptGeneratorCommand);
                        scriptGenerator.ExecuteWithProgressWriter(buildLogger, context.Tracer, scriptGeneratorCommand);
                    }
                    else
                    {
                        buildLogger.Log(Resources.Log_DeploymentScriptGeneratorUsingCache, scriptGeneratorCommand);
                    }

                    CacheDeploymentScript(scriptGeneratorCommand, context);
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
            string nextDeploymentPath = Path.GetDirectoryName(context.NextManifestFilePath);
            string cachedDeploymentScriptPath = Path.Combine(nextDeploymentPath, DeploymentScriptFileName);

            try
            {
                OperationManager.Attempt(() =>
                    File.Copy(Path.Combine(RepositoryPath, DeploymentScriptFileName), cachedDeploymentScriptPath, overwrite: true));

                string cachedDeploymentCommandArgumentsPath = Path.Combine(nextDeploymentPath, DeploymentCommandArgumentsFileName);
                File.WriteAllText(cachedDeploymentCommandArgumentsPath, scriptGeneratorCommand);

                context.Tracer.Trace("Saved cached version of the deployment script under: {0}", cachedDeploymentScriptPath);
            }
            catch (Exception ex)
            {
                // Do not fail the deployment on failure to save to cache but log the failure
                context.Tracer.Trace("Failed to save cached version of the deployment script under {0}, for command {1}", cachedDeploymentScriptPath, scriptGeneratorCommand);
                context.Tracer.TraceError(ex);
            }
        }

        private bool UseCachedDeploymentScript(string scriptGeneratorCommand, DeploymentContext context)
        {
                if (string.IsNullOrEmpty(scriptGeneratorCommand))
                {
                    // No previous deployment script generator command arguments means no previous deployment script
                    return false;
                }

                string previousDeploymentPath = Path.GetDirectoryName(context.PreviousManifestFilePath);
                string cachedDeploymentCommandArgumentsPath = Path.Combine(previousDeploymentPath, DeploymentCommandArgumentsFileName);
                string cachedDeploymentScriptPath = Path.Combine(previousDeploymentPath, DeploymentScriptFileName);

                try
                {
                    if (!File.Exists(cachedDeploymentCommandArgumentsPath))
                    {
                        return false;
                    }

                    string cachedDeploymentCommandArguments = File.ReadAllText(cachedDeploymentCommandArgumentsPath).Trim();

                    // If we use the same deployment script generator command
                    if (scriptGeneratorCommand == cachedDeploymentCommandArguments)
                    {
                        // And if there is a cached deployment script from previous deployment
                        if (File.Exists(cachedDeploymentScriptPath))
                        {
                            // Use the cached deployment script
                            OperationManager.Attempt(() =>
                                File.Copy(cachedDeploymentScriptPath, Path.Combine(RepositoryPath, DeploymentScriptFileName), overwrite: true));

                            context.Tracer.Trace("Using cached version of the deployment script from: {0}", cachedDeploymentScriptPath);

                            return true;
                        }
                    }

                    return false;
                }
                catch (Exception ex)
                {
                    // Do not fail the deployment on failure to use cache but log the failure
                    context.Tracer.Trace("Failed to use cached version of the deployment script found under: {0}", cachedDeploymentScriptPath);
                    context.Tracer.TraceError(ex);
                    return false;
                }
        }

        private string DeploymentScriptGeneratorToolPath
        {
            get
            {
                return Path.Combine(Environment.NodeModulesPath, ".bin", "azure.cmd");
            }
        }
    }
}

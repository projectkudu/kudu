using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class GeneratorSiteBuilder : ExternalCommandBuilder
    {
        private const string ScriptGeneratorCommandFormat = "site deploymentscript -y --no-dot-deployment -r \"{0}\" {1}";

        public GeneratorSiteBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath)
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
                RunCommand(context, "deploy.cmd");
                PostDeployScript(context);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                buildLogger.Log(ex);
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        protected virtual void PostDeployScript(DeploymentContext context)
        {
        }

        protected abstract string ScriptGeneratorCommandArguments { get; }

        /// <summary>
        /// Due to node issue, an argument cannot end with \ followed by a " (\"),
        /// This causes the arguments to be parsed incorrectly.
        /// </summary>
        protected string CleanPath(string path)
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
                    scriptGenerator.SetHomePath(HomePath);

                    var scriptGeneratorCommand = String.Format(ScriptGeneratorCommandFormat, RepositoryPath, ScriptGeneratorCommandArguments);
                    buildLogger.Log(Resources.Log_DeploymentScriptGeneratorCommand, scriptGeneratorCommand);

                    scriptGenerator.ExecuteWithProgressWriter(buildLogger, context.Tracer, _ => false, scriptGeneratorCommand);
                }
            }
            catch (CommandLineException ex)
            {
                context.Tracer.TraceError(ex);

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();

                // Add the output stream and the error stream to the log for better
                // debugging
                buildLogger.Log(ex.Output, LogEntryType.Error);
                buildLogger.Log(ex.Error, LogEntryType.Error);

                throw;
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

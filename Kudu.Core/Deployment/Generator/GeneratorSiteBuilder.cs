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
            ILogger buildLogger = context.Logger.Log(Resources.Log_BuildingWebProject, Path.GetFileName(RepositoryPath));

            try
            {
                PreBuild(context);
                GenerateScript(context, buildLogger);
                RunCommand(context, "deploy.cmd");
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                buildLogger.Log(ex);
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        protected virtual void PreBuild(DeploymentContext context)
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
            StringBuilder log = new StringBuilder();

            try
            {
                using (context.Tracer.Step("Generating deployment script"))
                {
                    var scriptGenerator = new Executable(AzureScriptPath, RepositoryPath);
                    scriptGenerator.SetHomePath(HomePath);

                    var scriptGeneratorCommand = String.Format(ScriptGeneratorCommandFormat, RepositoryPath, ScriptGeneratorCommandArguments);

                    using (var writer = new ProgressWriter())
                    {
                        writer.Start();

                        scriptGenerator.Execute(context.Tracer,
                                                   output =>
                                                   {
                                                       // TODO: Do we want those outputs?
                                                       writer.WriteOutLine(output);
                                                       log.AppendLine(output);
                                                       return true;
                                                   },
                                                   error =>
                                                   {
                                                       writer.WriteErrorLine(error);
                                                       log.AppendLine(error);
                                                       return true;
                                                   },
                                                   Console.OutputEncoding,
                                                   scriptGeneratorCommand);
                    }
                }
            }
            catch (Exception ex)
            {
                context.Tracer.TraceError(ex);

                // TODO: don't swallow exception here

                // HACK: Log an empty error to the global logger (post receive hook console output).
                // The reason we don't log the real exception is because the 'live output' running
                // msbuild has already been captured.
                context.GlobalLogger.LogError();
                context.Logger.Log(ex);
                throw;
            }
            finally
            {
                buildLogger.Log(log.ToString());
            }
        }

        private string AzureScriptPath
        {
            get
            {
                return Path.Combine(Environment.NodeModulesPath, ".bin", "azure.cmd");
            }
        }
    }
}

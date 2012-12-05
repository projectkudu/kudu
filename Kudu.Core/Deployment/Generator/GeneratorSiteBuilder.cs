using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace Kudu.Core.Deployment.Generator
{
    public abstract class GeneratorSiteBuilder : ExternalCommandBuilder
    {
        private const string ScriptGeneratorCommandFormat = "site deploymentscript -y --no-dot-deployment -r \"{0}\" {1}";

        private static readonly string ScriptGeneratorPath = Path.Combine(System.Environment.GetEnvironmentVariable(Constants.NodeModulesBinPathEnvKey), "azure.cmd");

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
                    var scriptGenerator = new Executable(ScriptGeneratorPath, RepositoryPath);
                    var scriptGeneratorCommand = String.Format(ScriptGeneratorCommandFormat, RepositoryPath, ScriptGeneratorCommandArguments);

                    using (var writer = new ProgressWriter())
                    {
                        writer.Start();

                        string log = scriptGenerator.Execute(context.Tracer,
                                                   output =>
                                                   {
                                                       // TODO: Do we want those outputs?
                                                       writer.WriteOutLine(output);
                                                       return true;
                                                   },
                                                   error =>
                                                   {
                                                       writer.WriteErrorLine(error);
                                                       return true;
                                                   },
                                                   Console.OutputEncoding,
                                                   scriptGeneratorCommand).Item1;

                        buildLogger.Log(log);
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
        }
    }
}

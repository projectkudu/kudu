using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Contracts.Settings;
using System.IO.Abstractions;

namespace Kudu.Core.Deployment.Generator
{
    public class CustomBuilder : ExternalCommandBuilder
    {
        private readonly string _command;

        public CustomBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string repositoryPath, string command)
            : base(environment, settings, propertyProvider, repositoryPath)
        {
            _command = command;
        }

        public override Task Build(DeploymentContext context)
        {
            var tcs = new TaskCompletionSource<object>();
            context.Logger.Log("Running custom deployment command...");

            try
            {
                RunCommand(context, _command);
                HandleNodeSite(context);
                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        private void HandleNodeSite(DeploymentContext context)
        {
            ILogger innerLogger = null;

            try
            {
                var fileSystem = new FileSystem();
                if (NodeSiteEnabler.LooksLikeNode(fileSystem, context.OutputPath))
                {
                    innerLogger = context.Logger.Log(Resources.Log_SelectNodeJsVersion);

                    // We use wwwroot as the source (and destination) since this is a custom deployment
                    // And we don't know where would the root of the site be in the source
                    // (package.json may not even exist in the source for this custom deployment scenario)
                    string log = NodeSiteEnabler.SelectNodeVersion(fileSystem, Environment.ScriptPath, context.OutputPath, context.OutputPath, DeploymentSettings, context.Tracer);

                    innerLogger.Log(log);
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

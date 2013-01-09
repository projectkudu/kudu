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

                // If the user deployed a node.js site, run the select node version logic on his site to use the correct node.exe
                HandleNodeSite(context);

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        /// <summary>
        /// Update iisnode.yml file to use the specific node engine depandant on packages.json file (node engine setting),
        /// This is only done for node.js sites.
        /// </summary>
        private void HandleNodeSite(DeploymentContext context)
        {
            var fileSystem = new FileSystem();
            if (NodeSiteEnabler.LooksLikeNode(fileSystem, context.OutputPath))
            {
                ILogger innerLogger = context.Logger.Log(Resources.Log_SelectNodeJsVersion);

                // We use wwwroot as the source (and destination) since this is a custom deployment
                // And we don't know where would the root of the site be in the source
                // (package.json may not even exist in the source for this custom deployment scenario)
                NodeSiteEnabler.SelectNodeVersion(fileSystem, Environment.ScriptPath, context.OutputPath, context.OutputPath, DeploymentSettings, context.Tracer, innerLogger);
            }
        }
    }
}

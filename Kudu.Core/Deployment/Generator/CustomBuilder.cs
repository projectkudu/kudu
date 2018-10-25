using System;
using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Helpers;

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
            string commandFullPath = _command;
            var tcs = new TaskCompletionSource<object>();
            context.Logger.Log("Running custom deployment command...");

            try
            {
                if (!OSDetector.IsOnWindows())
                {
                    if (commandFullPath.StartsWith(".", StringComparison.OrdinalIgnoreCase))
                    {
                        string finalCommandPath = Path.GetFullPath(Path.Combine(RepositoryPath, commandFullPath));
                        if (File.Exists(finalCommandPath))
                        {
                            commandFullPath = finalCommandPath;
                        }
                    }

                    if(commandFullPath.Contains(RepositoryPath))
                    {
                        context.Logger.Log("Setting execute permissions for " + commandFullPath);
                        PermissionHelper.Chmod("ugo+x", commandFullPath, Environment, DeploymentSettings, context.Logger);
                    }
                    else
                    {
                        context.Logger.Log("Not setting execute permissions for " + commandFullPath);
                    }
                }
                
                RunCommand(context, _command, ignoreManifest: false);

                tcs.SetResult(null);
            }
            catch (Exception ex)
            {
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        public override string ProjectType
        {
            get { return "CUSTOM DEPLOYMENT"; }
        }
    }
}

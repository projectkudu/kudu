using System.IO;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment.Generator
{
    /// <summary>
    /// Base class for a console worker type site builder.
    /// Derived from this class are:
    /// * BasicConsoleBuilder - Console worker consisting of files and a command line to run (as the worker)
    /// * DotNetConsoleBuilder - Console worker consisting a .net console application project which is built and the artifact executable will run as the worker
    /// </summary>
    public abstract class BaseConsoleBuilder : GeneratorSiteBuilder
    {
        protected BaseConsoleBuilder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectPath)
            : base(environment, settings, propertyProvider, sourcePath)
        {
            ProjectPath = projectPath;
        }

        protected virtual string Command
        {
            get { return DeploymentSettings.GetValue(SettingsKeys.WorkerCommand); }
        }

        protected string ProjectPath { get; private set; }

        public override async Task Build(DeploymentContext context)
        {
            // A console worker consists of the console worker site and the binaries to run.
            // Tree looks like so:
            // site\
            //   wwwroot\
            //     global.asax     (this contains the worker site's logic)
            //     web.config
            //   bin\
            //     run_worker.cmd  (the startup script for the worker process, this will contain the command to run as the worker)
            //     %worker files%  (these are the deployed user files to use for the worker process)

            string destinationPath = context.BuildTempPath;
            string consoleWorkerPath = Path.Combine(Environment.ScriptPath, "ConsoleWorker");

            CopyFileFromTemplate(consoleWorkerPath, destinationPath, "global.asax");
            CopyFileFromTemplate(consoleWorkerPath, destinationPath, "web.config");

            string scriptDirectoryPath = Path.Combine(destinationPath, "bin");
            string scriptFilePath = Path.Combine(scriptDirectoryPath, "run_worker.cmd");
            string scriptContent = "@echo off\n{0}\n".FormatInvariant(Command);

            OperationManager.Attempt(() =>
            {
                FileSystemHelpers.EnsureDirectory(scriptDirectoryPath);
                File.WriteAllText(scriptFilePath, scriptContent);
            });

            await base.Build(context);
        }

        private static void CopyFileFromTemplate(string sourcePath, string destinationPath, string fileName)
        {
            sourcePath = Path.Combine(sourcePath, fileName + ".template");
            destinationPath = Path.Combine(destinationPath, fileName);
            OperationManager.Attempt(() => File.Copy(sourcePath, destinationPath, overwrite: true));
        }
    }
}
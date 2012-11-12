using Kudu.Console.Services;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Kudu.Services;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;

namespace Kudu.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            // Turn flag on in app.config to wait for debugger on launch
            if (ConfigurationManager.AppSettings["WaitForDebuggerOnStart"] == "true")
            {
                while (!Debugger.IsAttached)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }

            if (args.Length < 2)
            {
                System.Console.WriteLine("Usage: kudu.exe appRoot wapTargets [deployer]");
                return 1;
            }

            // The post receive hook launches the exe from sh and intereprets newline differently.
            // This fixes very wacky issues with how the output shows up in the conosle on push
            System.Console.Error.NewLine = "\n";
            System.Console.Out.NewLine = "\n";

            System.Environment.SetEnvironmentVariable("GIT_DIR", null, System.EnvironmentVariableTarget.Process);

            var appRoot = args[0];
            var wapTargets = args[1];
            string deployer = args.Length == 2 ? null : args[2];
            string nugetCachePath = null;

            IEnvironment env = GetEnvironment(appRoot, nugetCachePath);

            // Setup the trace
            string tracePath = Path.Combine(env.RootPath, Constants.TracePath, Constants.TraceFile);
            var tracer = new Tracer(tracePath);
            var traceFactory = new TracerFactory(() => tracer);

            // Calculate the lock path
            string lockPath = Path.Combine(env.SiteRootPath, Constants.LockPath);
            string deploymentLockPath = Path.Combine(lockPath, Constants.DeploymentLockFile);
            var deploymentLock = new LockFile(traceFactory, deploymentLockPath);

            var fs = new FileSystem();
            var buildPropertyProvider = new BuildPropertyProvider();
            var serverRepository = new GitDeploymentRepository(env.RepositoryPath, traceFactory);
            var settings = new XmlSettings.Settings(GetSettingsPath(env));
            var settingsManager = new DeploymentSettingsManager(settings);
            var builderFactory = new SiteBuilderFactory(settingsManager, buildPropertyProvider, env);

            var logger = new ConsoleLogger();
            var deploymentManager = new DeploymentManager(serverRepository,
                                                          builderFactory, 
                                                          env, 
                                                          fs, 
                                                          traceFactory, 
                                                          settingsManager, 
                                                          deploymentLock, 
                                                          logger);

            var step = tracer.Step("Executing external process", new Dictionary<string, string>
            {
                { "type", "process" },
                { "path", "kudu.exe" },
                { "arguments", appRoot + " " + wapTargets }
            });

            using (step)
            {
                try
                {
                    deploymentManager.Deploy(deployer);
                }
                catch
                {
                    System.Console.Error.WriteLine(Resources.Log_DeploymentError);

                    throw;
                }
            }

            if (logger.HasErrors)
            {
                System.Console.Error.WriteLine(Resources.Log_DeploymentError);
                return 1;
            }

            return 0;
        }

        private static string GetSettingsPath(IEnvironment environment)
        {
            return Path.Combine(environment.DeploymentCachePath, Constants.DeploySettingsPath);
        }

        private static IEnvironment GetEnvironment(string siteRoot, string nugetCachePath)
        {
            string root = Path.GetFullPath(Path.Combine(siteRoot, ".."));
            string webRootPath = Path.Combine(siteRoot, Constants.WebRoot);
            string deployCachePath = Path.Combine(siteRoot, Constants.DeploymentCachePath);
            string sshKeyPath = Path.Combine(siteRoot, Constants.SSHKeyPath);
            string repositoryPath = Path.Combine(siteRoot, Constants.RepositoryPath);
            string tempPath = Path.GetTempPath();
            string deploymentTempPath = Path.Combine(tempPath, Constants.RepositoryPath);
            string binPath = new FileInfo(Process.GetCurrentProcess().MainModule.FileName).DirectoryName;
            string scriptPath = Path.Combine(binPath, Constants.ScriptsPath);

            return new Environment(new FileSystem(),
                                   root,
                                   siteRoot,
                                   tempPath,
                                   repositoryPath,
                                   webRootPath,
                                   deployCachePath,
                                   sshKeyPath,
                                   nugetCachePath,
                                   scriptPath);
        }
    }
}

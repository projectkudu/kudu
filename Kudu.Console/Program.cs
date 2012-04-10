using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using Kudu.Console.Services;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;

namespace Kudu.Console
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 2)
            {
                System.Console.WriteLine("Usage: kudu.exe {appRoot} {wapTargets}");
                return 1;
            }

            // The post receive hook launches the exe from sh and intereprets newline differently.
            // This fixes very wacky issues with how the output shows up in the conosle on push
            System.Console.Error.NewLine = "\n";
            System.Console.Out.NewLine = "\n";

            System.Environment.SetEnvironmentVariable("GIT_DIR", null, System.EnvironmentVariableTarget.Process);

            var appRoot = args[0];
            var wapTargets = args[1];
            string nugetCachePath = null;

            IEnvironment env = GetEnvironment(appRoot, nugetCachePath);

            // Setup the trace
            string tracePath = Path.Combine(env.ApplicationRootPath, Constants.TracePath, Constants.TraceFile);
            var tracer = new Tracer(tracePath);
            var traceFactory = new TracerFactory(() => tracer);

            // Calculate the lock path
            string lockPath = Path.Combine(env.ApplicationRootPath, Constants.LockPath);
            string deploymentLockPath = Path.Combine(lockPath, Constants.DeploymentLockFile);
            var deploymentLock = new LockFile(traceFactory, deploymentLockPath);

            var fs = new FileSystem();
            var buildPropertyProvider = new BuildPropertyProvider(wapTargets);
            var builderFactory = new SiteBuilderFactory(buildPropertyProvider, env);
            var serverRepository = new GitDeploymentRepository(env.DeploymentRepositoryPath, traceFactory);

            var logger = new ConsoleLogger();
            var deploymentManager = new DeploymentManager(serverRepository,
                                                          builderFactory,
                                                          env,
                                                          fs,
                                                          traceFactory,
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
                    deploymentManager.Deploy();
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

        private static IEnvironment GetEnvironment(string root, string nugetCachePath)
        {
            string deployPath = Path.Combine(root, Constants.WebRoot);
            string deployCachePath = Path.Combine(root, Constants.DeploymentCachePath);
            string deploymentRepositoryPath = Path.Combine(root, Constants.RepositoryPath);
            string tempPath = Path.GetTempPath();
            string deploymentTempPath = Path.Combine(tempPath, Constants.RepositoryPath);

            return new Environment(root,
                                   tempPath,
                                   () => deploymentRepositoryPath,
                                   () => null,
                                   deployPath,
                                   deployCachePath,
                                   nugetCachePath);
        }
    }
}

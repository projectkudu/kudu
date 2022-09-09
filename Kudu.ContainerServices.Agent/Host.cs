using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core;
using Kudu.Core.Helpers;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Jobs;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Kudu.ContainerServices.Agent
{
    public static class Host
    {

        public static ITriggeredJobsManager _triggeredJobsManager;
        public static IContinuousJobsManager _continuousJobsManager;
        public static TriggeredJobsScheduler _triggeredJobsScheduler;

        public static void Run(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;

            var serverConfiguration = new ServerConfiguration();
            var etwTraceFactory = new TracerFactory(() => new ETWTracer(string.Empty, string.Empty));

            string root = System.Environment.ExpandEnvironmentVariables(@"%HOME%");
            string siteRoot = Path.Combine(root, Constants.SiteFolder);
            string repositoryPath = Path.Combine(siteRoot, Constants.RepositoryPath);
            string binPath = AppDomain.CurrentDomain.BaseDirectory;
            string requestId = null;
            
            IEnvironment environment = new Core.Environment(root, EnvironmentHelper.NormalizeBinPath(binPath), repositoryPath, requestId);

            IDeploymentSettingsManager noContextDeploymentsSettingsManager =
                new DeploymentSettingsManager(new XmlSettings.Settings(Path.Combine(environment.DeploymentsPath, Constants.DeploySettingsPath)));

            var analytics = new Analytics(noContextDeploymentsSettingsManager,
                                                                        serverConfiguration,
                                                                        etwTraceFactory);

            string lockPath = Path.Combine(environment.SiteRootPath, Constants.LockPath);
            string deploymentLockPath = Path.Combine(lockPath, Constants.DeploymentLockFile);
            string statusLockPath = Path.Combine(lockPath, Constants.StatusLockFile);
            string hooksLockPath = Path.Combine(lockPath, Constants.HooksLockFile);
            var statusLock = new LockFile(statusLockPath, etwTraceFactory, traceLock: false);
            var hooksLock = new LockFile(hooksLockPath, etwTraceFactory);


            _triggeredJobsManager = new AggregateTriggeredJobsManager(
                etwTraceFactory,
                environment,
                noContextDeploymentsSettingsManager,
                analytics,
                new WebHooksManager(etwTraceFactory.GetTracer(), environment, hooksLock));

            _triggeredJobsScheduler = new TriggeredJobsScheduler(
                _triggeredJobsManager,
                etwTraceFactory,
                environment,
                noContextDeploymentsSettingsManager,
                analytics);

            _continuousJobsManager = new AggregateContinuousJobsManager(
                etwTraceFactory,
                environment,
                noContextDeploymentsSettingsManager,
                analytics);

            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder
                        .ConfigureServices(services =>
                        {
                            services.AddControllers();
                            services.Configure<KestrelServerOptions>(options =>
                            {
                                options.AllowSynchronousIO = true;
                            });
                        })
                        .UseKestrel(options =>
                        {
                            options.ListenAnyIP(port: 50555);
                        })
                        .UseStartup<Startup>();
                });

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                Exception exception = e.ExceptionObject as Exception;
                if (exception != null)
                {
                    Trace.TraceError("Unhandled Exception. Error: " + exception.ToString());
                }
            }
            finally
            {
                if (!e.IsTerminating)
                {
                    Exception exception = e.ExceptionObject as Exception;
                    if (exception != null)
                    {
                        int errorCode = Marshal.GetHRForException(exception);
                        System.Environment.Exit(errorCode);
                    }
                }
            }
        }
    }
}

using System.IO;
using System.IO.Abstractions;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Kudu.Services.Authorization;
using Kudu.Services.Deployment;
using Kudu.Services.Performance;
using Kudu.Services.Web.Services;
using Kudu.Services.Web.Tracing;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.Wcf;
using SignalR;
using SignalR.Hosting.AspNet;
using SignalR.Infrastructure;
using XmlSettings;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start
{
    public static class NinjectServices
    {
        private const string LockPath = "locks";
        private const string DeploymentLockFile = "deployments.lock";
        private const string InitLockFile = "init.lock";

        private const string DeploymentCachePath = "deployments";
        private const string TracePath = @"LogFiles\Git\trace";
        private const string DeploySettingsPath = "settings.xml";
        private const string TraceFile = "trace.xml";

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            CreateKernel();
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop()
        {
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();

            RegisterServices(kernel);

            KernelContainer.Kernel = kernel;
            return kernel;
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        private static void RegisterServices(IKernel kernel)
        {
            var serverConfiguration = new ServerConfiguration();
            var gitConfiguration = new RepositoryConfiguration
            {
                Username = AppSettings.GitUsername,
                Email = AppSettings.GitEmail
            };

            IEnvironment environment = GetEnvironment();
            var propertyProvider = new BuildPropertyProvider();

            // General
            kernel.Bind<HttpContextBase>().ToMethod(context => new HttpContextWrapper(HttpContext.Current));
            kernel.Bind<IBuildPropertyProvider>().ToConstant(propertyProvider);
            kernel.Bind<IEnvironment>().ToConstant(environment);
            kernel.Bind<IUserValidator>().To<SimpleUserValidator>().InSingletonScope();
            kernel.Bind<IServerConfiguration>().ToConstant(serverConfiguration);
            kernel.Bind<IFileSystem>().To<FileSystem>().InSingletonScope();
            kernel.Bind<RepositoryConfiguration>().ToConstant(gitConfiguration);

            if (AppSettings.TraceEnabled)
            {
                string tracePath = Path.Combine(environment.ApplicationRootPath, TracePath, TraceFile);
                System.Func<ITracer> createTracerThunk = () => new Tracer(tracePath);

                // First try to use the current request profiler if any, otherwise create a new one
                var traceFactory = new TracerFactory(() => TraceServices.CurrentRequestTracer ?? createTracerThunk());

                kernel.Bind<ITracer>().ToMethod(context => TraceServices.CurrentRequestTracer ?? NullTracer.Instance);
                kernel.Bind<ITraceFactory>().ToConstant(traceFactory);
                TraceServices.SetTraceFactory(createTracerThunk);
            }
            else
            {
                // Return No-op providers
                kernel.Bind<ITracer>().ToConstant(NullTracer.Instance).InSingletonScope();
                kernel.Bind<ITraceFactory>().ToConstant(NullTracerFactory.Instance).InSingletonScope();
            }


            // Setup the deployment lock
            string lockPath = Path.Combine(environment.ApplicationRootPath, LockPath);
            string deploymentLockPath = Path.Combine(lockPath, DeploymentLockFile);
            string initLockPath = Path.Combine(lockPath, InitLockFile);

            var deploymentLock = new LockFile(kernel.Get<ITraceFactory>(), deploymentLockPath);
            var initLock = new LockFile(kernel.Get<ITraceFactory>(), initLockPath);

            kernel.Bind<IOperationLock>().ToConstant(deploymentLock);

            // Setup the diagnostics service to collect information from the following paths:
            // 1. The deployments folder
            // 2. The elmah error log
            // 3. The profile dump
            var paths = new[] { 
                environment.DeploymentCachePath,
                Path.Combine(environment.ApplicationRootPath, TracePath),
            };

            kernel.Bind<DiagnosticsService>().ToMethod(context => new DiagnosticsService(paths));


            // Deployment Service
            kernel.Bind<ISettings>().ToMethod(context => new XmlSettings.Settings(GetSettingsPath(environment)));

            kernel.Bind<IDeploymentSettingsManager>().To<DeploymentSettingsManager>();

            kernel.Bind<ISiteBuilderFactory>().To<SiteBuilderFactory>()
                                             .InRequestScope();

            kernel.Bind<IServerRepository>().ToMethod(context => new GitExeServer(environment.DeploymentRepositoryPath, initLock, context.Kernel.Get<ITraceFactory>()))
                                            .InRequestScope();

            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InRequestScope()
                                             .OnActivation(SubscribeForDeploymentEvents);

            // Git server
            kernel.Bind<IDeploymentManagerFactory>().ToMethod(context => GetDeploymentManagerFactory(environment, initLock, deploymentLock, propertyProvider, context.Kernel.Get<ITraceFactory>()));

            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(environment.DeploymentRepositoryPath, initLock, context.Kernel.Get<ITraceFactory>()))
                                     .InRequestScope();
 
            // Editor
            kernel.Bind<IProjectSystem>().ToMethod(context => GetEditorProjectSystem(environment, context))
                                         .InRequestScope();
        }

        private static IDeploymentManagerFactory GetDeploymentManagerFactory(IEnvironment environment,
                                                                             IOperationLock initLock,
                                                                             IOperationLock deploymentLock,
                                                                             IBuildPropertyProvider propertyProvider,
                                                                             ITraceFactory traceFactory)
        {
            return new DeploymentManagerFactory(() =>
            {
                var serverRepository = new GitExeServer(environment.DeploymentRepositoryPath, initLock, traceFactory);
                var fileSystem = new FileSystem();
                var siteBuilderFactory = new SiteBuilderFactory(propertyProvider, environment);

                var deploymentManager = new DeploymentManager(serverRepository,
                                                              siteBuilderFactory,
                                                              environment,
                                                              fileSystem,
                                                              traceFactory,
                                                              deploymentLock);

                SubscribeForDeploymentEvents(deploymentManager);

                return deploymentManager;
            });
        }

        private static IProjectSystem GetEditorProjectSystem(IEnvironment environment, IContext context)
        {
            return new ProjectSystem(environment.DeploymentTargetPath);
        }

        private static string GetSettingsPath(IEnvironment environment)
        {
            return Path.Combine(environment.DeploymentCachePath, DeploySettingsPath);
        }

        private static IEnvironment GetEnvironment()
        {
            string targetRoot = PathResolver.ResolveRootPath();

            string site = HttpRuntime.AppDomainAppVirtualPath.Trim('/');
            string root = Path.Combine(targetRoot, site);
            string deployPath = Path.Combine(root, Constants.WebRoot);
            string deployCachePath = Path.Combine(root, DeploymentCachePath);
            string deploymentRepositoryPath = Path.Combine(root, Constants.RepositoryPath);
            string tempPath = Path.GetTempPath();
            string deploymentTempPath = Path.Combine(tempPath, Constants.RepositoryPath);

            return new Environment(site,
                                   root,
                                   tempPath,
                                   () => deploymentRepositoryPath,
                                   () => ResolveRepositoryPath(site),
                                   deployPath,
                                   deployCachePath,
                                   AppSettings.NuGetCachePath);
        }

        private static string ResolveRepositoryPath(string site)
        {
            string path = PathResolver.ResolveDevelopmentPath();

            if (System.String.IsNullOrEmpty(path))
            {
                return null;
            }

            return Path.Combine(path, site, Constants.WebRoot);
        }

        private static void SubscribeForDeploymentEvents(IDeploymentManager deploymentManager)
        {
            IConnection connection = AspNetHost.DependencyResolver
                                               .Resolve<IConnectionManager>()
                                               .GetConnection<DeploymentStatusConnection>();

            deploymentManager.StatusChanged += status =>
            {
                connection.Broadcast(status);
            };
        }

        private class DeploymentManagerFactory : IDeploymentManagerFactory
        {
            private readonly System.Func<IDeploymentManager> _factory;
            public DeploymentManagerFactory(System.Func<IDeploymentManager> factory)
            {
                _factory = factory;
            }

            public IDeploymentManager CreateDeploymentManager()
            {
                return _factory();
            }
        }
    }
}

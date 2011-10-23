using System.IO;
using System.IO.Abstractions;
using System.Web;
using Kudu.Core;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.Hg;
using Kudu.Services.Authorization;
using Kudu.Services.Deployment;
using Kudu.Services.SourceControl;
using Kudu.Services.Web.Services;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Activation;
using Ninject.Extensions.Wcf;
using SignalR;
using XmlSettings;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start {
    public static class NinjectServices {
        private static CommandExecutor _devExecutor;
        private static CommandExecutor _liveExecutor;

        private const string RepositoryPath = "repository";
        private const string DeploymentTargetPath = "wwwroot";
        private const string DeploymentCachePath = "deployments";
        private const string DeploySettingsPath = "settings.xml";

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start() {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            CreateKernel();
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop() {
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel() {
            var kernel = new StandardKernel();

            RegisterServices(kernel);

            KernelContainer.Kernel = kernel;
            return kernel;
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        private static void RegisterServices(IKernel kernel) {
            IEnvironment environment = GetEnvironment();
            var propertyProvider = new BuildPropertyProvider();

            // General
            kernel.Bind<HttpContextBase>().ToMethod(context => new HttpContextWrapper(HttpContext.Current));
            kernel.Bind<IBuildPropertyProvider>().ToConstant(propertyProvider);
            kernel.Bind<IEnvironment>().ToConstant(environment);
            kernel.Bind<IUserValidator>().To<SimpleUserValidator>().InSingletonScope();
            kernel.Bind<IServerConfiguration>().To<ServerConfiguration>().InSingletonScope();
            kernel.Bind<IFileSystem>().To<FileSystem>().InSingletonScope();

            // Deployment
            kernel.Bind<IRepositoryManager>().ToMethod(context => new RepositoryManager(environment.DeploymentRepositoryPath));
            kernel.Bind<ISiteBuilderFactory>().To<SiteBuilderFactory>();
            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InSingletonScope()
                                             .OnActivation(SubscribeForDeploymentEvents);

            // Settings
            kernel.Bind<ISettings>().ToMethod(context => new XmlSettings.Settings(GetSettingsPath(environment)));
            kernel.Bind<IDeploymentSettingsManager>().To<DeploymentSettingsManager>();

            // Git server
            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(environment.DeploymentRepositoryPath))
                                     .InSingletonScope();

            // Hg Server
            kernel.Bind<IHgServer>().To<Kudu.Core.SourceControl.Hg.HgServer>()
                                    .InSingletonScope();

            // Editor
            kernel.Bind<IProjectSystem>().ToMethod(context => GetEditorProjectSystem(environment, context))
                                            .InRequestScope();

            // Command line
            kernel.Bind<ICommandExecutor>().ToMethod(context => GetComandExecutor(environment, context))
                                           .InRequestScope();

            // Source control
            kernel.Bind<IRepository>().ToMethod(context => GetSourceControlRepository(environment));
            kernel.Bind<IRepositoryManager>().ToMethod(context => GetDevelopmentRepositoryManager(environment))
                                            .WhenInjectedInto<CloneService>();
        }

        private static IRepositoryManager GetDevelopmentRepositoryManager(IEnvironment environment) {
            EnsureDevelopmentRepository(environment);

            return new RepositoryManager(environment.RepositoryPath);
        }

        private static IRepository GetSourceControlRepository(IEnvironment environment) {
            return GetDevelopmentRepositoryManager(environment).GetRepository();
        }

        private static IProjectSystem GetEditorProjectSystem(IEnvironment environment, IContext context) {
            if (IsDevSiteRequest(context)) {
                EnsureDevelopmentRepository(environment);
                return new ProjectSystem(environment.RepositoryPath);
            }

            return new ProjectSystem(environment.DeploymentTargetPath);
        }

        private static CommandExecutor GetComandExecutor(IEnvironment environment, IContext context) {
            var fileSystem = context.Kernel.Get<IFileSystem>();

            if (IsDevSiteRequest(context)) {
                EnsureDevelopmentRepository(environment);

                if (_devExecutor == null) {
                    _devExecutor = new CommandExecutor(fileSystem, environment.RepositoryPath);
                    SubscribeForCommandEvents<DevCommandStatusHandler>(_devExecutor);
                }

                return _devExecutor;
            }

            if (_liveExecutor == null) {
                _liveExecutor = new CommandExecutor(fileSystem, environment.DeploymentTargetPath);
                SubscribeForCommandEvents<LiveCommandStatusHandler>(_liveExecutor);
            }

            return _liveExecutor;
        }

        private static string GetSettingsPath(IEnvironment environment) {
            return Path.Combine(environment.DeploymentCachePath, DeploySettingsPath);
        }

        private static IEnvironment GetEnvironment() {
            string targetRoot = PathResolver.ResolveRootPath();
            InitializeEnvVars(targetRoot);

            string site = HttpRuntime.AppDomainAppVirtualPath.Trim('/');
            string root = Path.Combine(targetRoot, site);
            string deploymentRepositoryPath = Path.Combine(root, RepositoryPath);
            string deployPath = Path.Combine(root, DeploymentTargetPath);
            string deployCachePath = Path.Combine(root, DeploymentCachePath);

            return new Environment(site,
                                   root,
                                   deploymentRepositoryPath,
                                   () => {
                                       string path = PathResolver.ResolveDevelopmentPath();
                                       if (System.String.IsNullOrEmpty(path)) {
                                           return null;
                                       }
                                       return Path.Combine(path, site, DeploymentTargetPath);
                                   },
                                   deployPath,
                                   deployCachePath);
        }

        private static void InitializeEnvVars(string root) {
            string tempPath = Path.GetFullPath(Path.Combine(root, "_temp"));

            // Setup some temp variables
            FileSystemHelpers.EnsureDirectory(tempPath);

            System.Environment.SetEnvironmentVariable("TEMP", tempPath);
            System.Environment.SetEnvironmentVariable("TMP", tempPath);
        }

        private static bool IsDevSiteRequest(IContext context) {
            var httpContext = context.Kernel.Get<HttpContextBase>();
            return !httpContext.Request.RequestContext.RouteData.Values.ContainsKey("live");
        }

        private static void EnsureDevelopmentRepository(IEnvironment environment) {
            if (System.String.IsNullOrEmpty(environment.RepositoryPath)) {
                throw new System.InvalidOperationException("Developer mode not enabled for this site");
            }
        }

        private static void SubscribeForCommandEvents<T>(ICommandExecutor commandExecutor) where T : PersistentConnection {
            IConnection connection = Connection.GetConnection<T>();
            commandExecutor.CommandEvent += commandEvent => {
                connection.Broadcast(commandEvent);
            };
        }

        private static void SubscribeForDeploymentEvents(IDeploymentManager deploymentManager) {
            IConnection connection = Connection.GetConnection<DeploymentStatusHandler>();
            deploymentManager.StatusChanged += status => {
                connection.Broadcast(status);
            };
        }
    }
}

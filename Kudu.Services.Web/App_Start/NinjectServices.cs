using System.Configuration;
using System.IO;
using System.Web;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SourceControl.Hg;
using Kudu.Services.Authorization;
using Kudu.Services.Deployment;
using Kudu.Services.Web.Services;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Mvc;
using SignalR;
using XmlSettings;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start {
    public static class NinjectServices {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();
        private const string RepositoryPath = "repository";
        private const string DeploymentTargetPath = "wwwroot";
        private const string DeploymentCachePath = "deployments";
        private const string DeploySettingsPath = "settings.xml";

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start() {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            DynamicModuleUtility.RegisterModule(typeof(HttpApplicationInitializationModule));
            bootstrapper.Initialize(CreateKernel);
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop() {
            bootstrapper.ShutDown();
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel() {
            var kernel = new StandardKernel();

            RegisterServices(kernel);
            return kernel;
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        private static void RegisterServices(IKernel kernel) {
            IEnvironment environment = GetEnvironment();
            var propertyProvider = new BuildPropertyProvider();

            kernel.Bind<IBuildPropertyProvider>().ToConstant(propertyProvider);
            kernel.Bind<IEnvironment>().ToConstant(environment);
            kernel.Bind<IRepositoryManager>().ToMethod(context => new RepositoryManager(environment.RepositoryPath));
            kernel.Bind<ISiteBuilderFactory>().To<SiteBuilderFactory>();
            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InSingletonScope()
                                             .OnActivation(SubscribeForDeploymentEvents);

            kernel.Bind<IFileSystemFactory>().To<FileSystemFactory>();
            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(environment.RepositoryPath));
            kernel.Bind<IUserValidator>().To<SimpleUserValidator>();
            kernel.Bind<IHgServer>().To<Kudu.Core.SourceControl.Hg.HgServer>().InSingletonScope();
            kernel.Bind<IServerConfiguration>().To<ServerConfiguration>().InSingletonScope();

            string deploySettingsPath = Path.Combine(environment.DeploymentCachePath, DeploySettingsPath);
            kernel.Bind<ISettings>().ToMethod(context => new XmlSettings.Settings(deploySettingsPath));
        }

        private static string Root {
            get {
                string path = ConfigurationManager.AppSettings["app"];
                if (System.String.IsNullOrEmpty(path)) {
                    path = Path.Combine("..", "apps");
                }
                return Path.GetFullPath(Path.Combine(HttpRuntime.AppDomainAppPath, path));
            }
        }

        private static IEnvironment GetEnvironment() {
            InitializeEnvVars(Root);

            string site = HttpRuntime.AppDomainAppVirtualPath.Trim('/');
            string root = Path.Combine(Root, site);
            string repositoryPath = Path.Combine(root, RepositoryPath);
            string deployPath = Path.Combine(root, DeploymentTargetPath);
            string deployCachePath = Path.Combine(root, DeploymentCachePath);

            return new Environment(site, root, repositoryPath, deployPath, deployCachePath);
        }

        private static void InitializeEnvVars(string root) {
            string tempPath = Path.GetFullPath(Path.Combine(root, "_temp"));

            // Setup some temp variables
            FileSystemHelpers.EnsureDirectory(tempPath);

            System.Environment.SetEnvironmentVariable("TEMP", tempPath);
            System.Environment.SetEnvironmentVariable("TMP", tempPath);
        }

        private static void SubscribeForDeploymentEvents(IDeploymentManager deploymentManager) {
            deploymentManager.StatusChanged += status => {
                IConnection connection = Connection.GetConnection<DeploymentStatusHandler>();
                connection.Broadcast(status);
            };
        }
    }
}

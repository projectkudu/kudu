using System.Configuration;
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

            kernel.Bind<ISettings>().ToMethod(context => new XmlSettings.Settings(GetSettingsPath(environment)));
            kernel.Bind<IBuildPropertyProvider>().ToConstant(propertyProvider);
            kernel.Bind<IEnvironment>().ToConstant(environment);
            kernel.Bind<IRepositoryManager>().ToMethod(context => new RepositoryManager(environment.RepositoryPath));
            kernel.Bind<ISiteBuilderFactory>().To<SiteBuilderFactory>();
            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InSingletonScope()
                                             .OnActivation(SubscribeForDeploymentEvents);

            kernel.Bind<IDeploymentSettingsManager>().To<DeploymentSettingsManager>();
            kernel.Bind<IEditorFileSystemFactory>().To<FileSystemFactory>();
            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(environment.RepositoryPath));
            kernel.Bind<IUserValidator>().To<SimpleUserValidator>();
            kernel.Bind<IHgServer>().To<Kudu.Core.SourceControl.Hg.HgServer>().InSingletonScope();
            kernel.Bind<IServerConfiguration>().To<ServerConfiguration>().InSingletonScope();
            kernel.Bind<IFileSystem>().To<FileSystem>();
            kernel.Bind<ICommandExecutor>().ToMethod(context => GetComandExecutor(environment, context))
                                           .InSingletonScope()
                                           .OnActivation(SubscribeForCommandEvents);
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

        private static CommandExecutor GetComandExecutor(IEnvironment environment, IContext context) {
            return new CommandExecutor(context.Kernel.Get<IFileSystem>(),
                                       environment.RepositoryPath);
        }

        private static string GetSettingsPath(IEnvironment environment) {
            return Path.Combine(environment.DeploymentCachePath, DeploySettingsPath);
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

        private static void SubscribeForCommandEvents(ICommandExecutor commandExecutor) {
            IConnection connection = Connection.GetConnection<CommandStatusHandler>();
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

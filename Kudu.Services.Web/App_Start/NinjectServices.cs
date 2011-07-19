using System.IO;
using System.Web;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Mvc;
using ServerRepository = Kudu.Services.GitServer.Repository;
using Kudu.Services.Authorization;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start {
    public static class NinjectServices {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();
        private const string RepositoryPath = "repository";
        private const string DeploymentTargetPath = "wwwroot";
        private const string BuildOutputPath = "builds";

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
            string repositoryPath = Path.Combine(Root, RepositoryPath);
            string deployPath = Path.Combine(Root, DeploymentTargetPath);
            string buildPath = Path.Combine(Root, BuildOutputPath);

            var environment = new Environment(repositoryPath, deployPath, buildPath);

            var repositoryManager = new RepositoryManager(environment.RepositoryPath);
            var deployerFactory = new DeployerFactory(environment);
            var deploymentManager = new DeploymentManager(repositoryManager, deployerFactory);
            var fileSystemFactory = new FileSystemFactory(environment);

            kernel.Bind<IRepositoryManager>().ToConstant(repositoryManager);
            kernel.Bind<IDeployerFactory>().ToConstant(deployerFactory);
            kernel.Bind<IDeploymentManager>().ToConstant(deploymentManager);
            kernel.Bind<IFileSystemFactory>().ToConstant(fileSystemFactory);
            kernel.Bind<ServerRepository>().ToMethod(_ => new ServerRepository(repositoryPath));
            kernel.Bind<IUserValidator>().To<SimpleUserValidator>();
        }

        private static string Root {
            get {
                return Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "_root");
            }
        }
    }
}

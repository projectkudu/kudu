using System.IO;
using System.Web;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Mvc;
using ServerRepository = Kudu.Services.GitServer.Repository;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start {    
    public static class NinjectServices {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();
        private const string RepositoryPath = "repository";
        private const string DeploymentTargetPath = "wwwroot";

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
            string repositoryPath = GetRepositoryPath();
            string deployPath = GetDeployRoot();

            var repositoryManager = new RepositoryManager(repositoryPath);
            var deploymentManager = new DeploymentManager(repositoryPath, deployPath);
            // TODO: We might want this factory to have more knowledge of all the paths in the system
            var fileSystemFactory = new FileSystemFactory(repositoryPath, deployPath);

            kernel.Bind<IRepositoryManager>().ToConstant(repositoryManager);
            kernel.Bind<IDeploymentManager>().ToConstant(deploymentManager);
            kernel.Bind<IDeployerFactory>().ToConstant(deploymentManager);
            kernel.Bind<IFileSystemFactory>().ToConstant(fileSystemFactory);
            kernel.Bind<ServerRepository>().ToMethod(_ => new ServerRepository(repositoryPath));
        }
        
        private static string Root {
            get {
                return Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "_root");
            }
        }

        private static string GetRepositoryPath() {
            string path = Path.Combine(Root, RepositoryPath);
            EnsureDirectory(path);
            return path;
        }

        private static string GetDeployRoot() {
            string path = Path.Combine(Root, DeploymentTargetPath);
            EnsureDirectory(path);
            return path;
        }

        private static void EnsureDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
        }
    }
}

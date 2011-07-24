using System.IO;
using System.Web;
using System.Web.Routing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Kudu.Services.Authorization;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Activation;
using Ninject.Web.Mvc;
using ServerRepository = Kudu.Services.GitServer.Repository;

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
            kernel.Bind<IEnvironment>().ToMethod(_ => GetEnvironment());
            kernel.Bind<IRepositoryManager>().ToMethod(context => new RepositoryManager(context.Kernel.Get<IEnvironment>().RepositoryPath));
            kernel.Bind<IDeployerFactory>().To<DeployerFactory>();
            kernel.Bind<IDeploymentManager>().To<DeploymentManager>();
            kernel.Bind<IFileSystemFactory>().To<FileSystemFactory>();
            kernel.Bind<ServerRepository>().ToMethod(context => new ServerRepository(context.Kernel.Get<IEnvironment>().RepositoryPath));
            kernel.Bind<IUserValidator>().To<SimpleUserValidator>();
        }

        private static string Root {
            get {
                return Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "apps");
            }
        }

        private static IEnvironment GetEnvironment() {
            string site = HttpRuntime.AppDomainAppVirtualPath.Trim('/');
            string root = Path.Combine(Root, site);
            string repositoryPath = Path.Combine(root, RepositoryPath);
            string deployPath = Path.Combine(root, DeploymentTargetPath);
            string buildPath = Path.Combine(root, BuildOutputPath);

            return new Environment(repositoryPath, deployPath, buildPath);

        }
    }
}

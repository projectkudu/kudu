using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Mvc;
using SignalR.Infrastructure;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Web.App_Start {
    public static class NinjectServices {
        private const string ServiceUrl = "http://localhost:52590/";
        private const string FilesService = ServiceUrl + "files";
        private const string ScmService = ServiceUrl + "scm";
        private const string DeploymentService = ServiceUrl + "deploy";

        private static readonly Bootstrapper bootstrapper = new Bootstrapper();

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start() {
            // Resolver for signalr
            var kernel = CreateKernel();
            DependencyResolver.SetResolver(new NinjectDependencyResolver(kernel));

            // Resolver for mvc3
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
            kernel.Bind<IFileSystem>().ToConstant(new RemoteFileSystem(FilesService));
            kernel.Bind<IRepository>().ToConstant(new RemoteRepository(ScmService));
            kernel.Bind<IRepositoryManager>().ToConstant(new RemoteRepositoryManager(ScmService));
            var deploymentManager = new RemoteDeploymentManager(DeploymentService);
            kernel.Bind<IDeploymentManager>().ToConstant(deploymentManager);
            kernel.Bind<IDeployer>().ToConstant(deploymentManager);
        }
    }
}

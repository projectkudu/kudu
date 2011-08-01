using System.Web;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Kudu.Web.Infrastructure;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Activation;
using Ninject.Web.Mvc;
using SignalR.Infrastructure;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Web.App_Start {
    public static class NinjectServices {
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
            kernel.Bind<ISiteManager>().ToConstant(new SiteManager());
            kernel.Bind<HttpContextBase>().ToMethod(_ => new HttpContextWrapper(HttpContext.Current));
            kernel.Bind<ISiteConfiguration>().To<SiteConfiguration>();
            kernel.Bind<IFileSystem>().ToMethod(context => GetFileSystem(context));
            kernel.Bind<IRepository>().ToMethod(context => GetRepository(context));
            kernel.Bind<IRepositoryManager>().ToMethod(context => GetRepositoryManager(context));
            kernel.Bind<IDeploymentManager>().ToMethod(context => GetDeploymentManager(context));
        }

        private static IRepository GetRepository(IContext context) {
            var siteConfiguraiton = context.Kernel.Get<ISiteConfiguration>();
            return siteConfiguraiton.Repository;
        }

        private static IFileSystem GetFileSystem(IContext context) {
            var siteConfiguraiton = context.Kernel.Get<ISiteConfiguration>();
            return siteConfiguraiton.FileSystem;
        }

        private static IDeploymentManager GetDeploymentManager(IContext context) {
            var siteConfiguraiton = context.Kernel.Get<ISiteConfiguration>();
            return siteConfiguraiton.DeploymentManager;
        }

        private static IRepositoryManager GetRepositoryManager(IContext context) {
            var siteConfiguraiton = context.Kernel.Get<ISiteConfiguration>();
            return siteConfiguraiton.RepositoryManager;
        }
    }
}

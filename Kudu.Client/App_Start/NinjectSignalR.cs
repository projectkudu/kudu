using Ninject;
using SignalR.Infrastructure;
using SignalR.Ninject;
using Kudu.Core.SourceControl;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Client.Infrastructure;
using Ninject.Activation;
using System.Web;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Client.App_Start.NinjectSignalR), "Start")]

namespace Kudu.Client.App_Start {
    public static class NinjectSignalR {
        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start() {
            IKernel kernel = CreateKernel();
            DependencyResolver.SetResolver(new NinjectDependencyResolver(kernel));
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
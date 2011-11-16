using System.Configuration;
using System.IO;
using System.Web;
using Kudu.Client.Infrastructure;
using Kudu.SiteManagement;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Web.Mvc;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Web.App_Start.NinjectMvc3Services), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Web.App_Start.NinjectMvc3Services), "Stop")]

namespace Kudu.Web.App_Start
{
    public static class NinjectMvc3Services
    {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            // Resolver for mvc3
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            DynamicModuleUtility.RegisterModule(typeof(HttpApplicationInitializationModule));
            bootstrapper.Initialize(CreateKernel);
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop()
        {
            bootstrapper.ShutDown();
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();
            RegisterServices(kernel);
            return kernel;
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        private static void RegisterServices(IKernel kernel)
        {
            string sitePath = HttpRuntime.AppDomainAppPath;
            string serviceSitePath = ConfigurationManager.AppSettings["serviceSitePath"];
            string sitesPath = ConfigurationManager.AppSettings["sitesPath"];

            serviceSitePath = Path.Combine(sitePath, serviceSitePath);
            sitesPath = Path.Combine(sitePath, sitesPath);

            kernel.Bind<IPathResolver>().ToConstant(new DefaultPathResolver(serviceSitePath, sitesPath));
            kernel.Bind<ISiteManager>().To<SiteManager>();
            kernel.Bind<ICredentialProvider>().ToConstant(new BasicAuthCredentialProvider("admin", "kudu"));
        }
    }
}

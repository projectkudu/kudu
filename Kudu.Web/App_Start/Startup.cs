using System.Configuration;
using System.IO;
using System.Web;
using Kudu.Client.Infrastructure;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using Ninject;
using Ninject.Web.Mvc;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Web.App_Start.Startup), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Web.App_Start.Startup), "Stop")]

namespace Kudu.Web.App_Start
{
    public static class Startup
    {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            // Resolver for mvc3
            HttpApplication.RegisterModule(typeof(OnePerRequestModule));
            HttpApplication.RegisterModule(typeof(HttpApplicationInitializationModule));
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
            SetupKuduServices(kernel);
        }

        private static void SetupKuduServices(IKernel kernel)
        {
            string root = HttpRuntime.AppDomainAppPath;
            string serviceSitePath = ConfigurationManager.AppSettings["serviceSitePath"];
            string sitesPath = ConfigurationManager.AppSettings["sitesPath"];
            string sitesBaseUrl = ConfigurationManager.AppSettings["urlBaseValue"];
            string serviceSitesBaseUrl = ConfigurationManager.AppSettings["serviceUrlBaseValue"];

            serviceSitePath = Path.Combine(root, serviceSitePath);
            sitesPath = Path.Combine(root, sitesPath);

            var pathResolver = new DefaultPathResolver(serviceSitePath, sitesPath);
            var settingsResolver = new DefaultSettingsResolver(sitesBaseUrl, serviceSitesBaseUrl);

            kernel.Bind<IPathResolver>().ToConstant(pathResolver);
            kernel.Bind<ISettingsResolver>().ToConstant(settingsResolver);
            kernel.Bind<ISiteManager>().To<SiteManager>().InSingletonScope();
            kernel.Bind<KuduEnvironment>().ToMethod(_ => new KuduEnvironment
            {
                RunningAgainstLocalKuduService = true,
                IsAdmin = IdentityHelper.IsAnAdministrator(),
                ServiceSitePath = pathResolver.ServiceSitePath,
                SitesPath = pathResolver.SitesPath
            });

            // TODO: Integrate with membership system
            kernel.Bind<ICredentialProvider>().ToConstant(new BasicAuthCredentialProvider("admin", "kudu"));
            kernel.Bind<IApplicationService>().To<ApplicationService>().InRequestScope();
            kernel.Bind<ISettingsService>().To<SettingsService>();

            // Sql CE setup
            Directory.CreateDirectory(Path.Combine(root, "App_Data"));
        }
    }
}

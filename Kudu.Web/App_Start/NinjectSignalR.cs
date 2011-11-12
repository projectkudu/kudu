using System.Web;
using Kudu.Client.Infrastructure;
using Kudu.SignalR;
using Kudu.SignalR.Infrastructure;
using Kudu.SignalR.Models;
using Kudu.Web.Infrastructure;
using Kudu.Web.Models;
using Ninject;
using Ninject.Activation;
using SignalR.Infrastructure;
using SignalR.Ninject;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Web.App_Start.NinjectSignalR), "Start")]

namespace Kudu.Web.App_Start
{
    public static class NinjectSignalR
    {
        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            IKernel kernel = CreateKernel();
            DependencyResolver.SetResolver(new NinjectDependencyResolver(kernel));

            // Initialize kudu's bindings
            KuduDefaultBindings.Initialize();
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
            kernel.Bind<HttpContextBase>().ToMethod(_ => new HttpContextWrapper(HttpContext.Current));
            kernel.Bind<IApplication>().ToMethod(context => GetApplication(context));
            kernel.Bind<ICredentialProvider>().ToConstant(new BasicAuthCredentialProvider("admin", "kudu"));
            kernel.Bind<IUserInformation>().ToMethod(_ => new UserInformation
            {
                UserName = "Test <foo@test.com>"
            });
        }

        private static IApplication GetApplication(IContext context)
        {
            var httpContext = context.Kernel.Get<HttpContextBase>();
            var state = ClientStateResolver.GetState(httpContext);
            var applicationName = (string)state["applicationName"];

            using (var db = new KuduContext())
            {
                return db.Applications.Find(applicationName);
            }
        }
    }
}
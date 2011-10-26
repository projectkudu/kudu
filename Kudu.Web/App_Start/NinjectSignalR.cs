using System.Web;
using Kudu.SignalR;
using Kudu.SignalR.Infrastructure;
using Kudu.SignalR.Models;
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
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel(new DefaultBindings());
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
            kernel.Bind<IUserInformation>().ToMethod(_ => new UserInformation
            {
                UserName = "Test <foo@test.com>"
            });
        }

        private static IApplication GetApplication(IContext context)
        {
            var httpContext = context.Kernel.Get<HttpContextBase>();
            string applicationName = ApplicationNameResolver.ResolveName(httpContext);
            using (var db = new KuduContext())
            {
                return db.Applications.Find(applicationName);
            }
        }
    }
}
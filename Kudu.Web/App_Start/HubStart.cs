using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Ninject;
using SignalR.Infrastructure;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Web.App_Start.HubStart), "Start")]

namespace Kudu.Web.App_Start {
    public static class HubStart {
        private const string ServiceUrl = "http://localhost:52590/";
        private const string FilesService = ServiceUrl + "files";
        private const string ScmService = ServiceUrl + "scm";

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start() {
            var kernel = CreateKernel();
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
            kernel.Bind<IFileSystem>().ToConstant(new RemoteFileSystem(FilesService));
            kernel.Bind<IRepository>().ToConstant(new RemoteRepository(ScmService));
        }
    }
}

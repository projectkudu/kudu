[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start {
    using System.IO;
    using System.Linq;
    using System.Web;
    using Kudu.Core.Editor;
    using Kudu.Core.SourceControl;
    using Kudu.Core.SourceControl.Git;
    using Kudu.Core.SourceControl.Hg;
    using Microsoft.Web.Infrastructure.DynamicModuleHelper;
    using Ninject;
    using Ninject.Web.Mvc;
    using ServerRepository = GitServer.Repository;

    public static class NinjectServices {
        private static readonly Bootstrapper bootstrapper = new Bootstrapper();
        private const string RepositoryPath = "repository";

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
            IRepositoryManager repositoryManager = GetRepositoryManager();

            kernel.Bind<IRepositoryManager>().ToConstant(repositoryManager);
            kernel.Bind<IRepository>().ToMethod(_ => repositoryManager.GetRepository() ?? NullRepository.Instance);
            kernel.Bind<IFileSystem>().ToMethod(_ => GetFileSystem());
            kernel.Bind<ServerRepository>().ToMethod(_ => GetServerRepository());
        }

        private static IRepositoryManager GetRepositoryManager() {
            return new RepositoryManager(GetRepositoryPath());
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

        private static string GetEditorPath() {
            string path = Path.Combine(Root, RepositoryPath);
            EnsureDirectory(path);
            return path;
        }

        private static void EnsureDirectory(string path) {
            if (!Directory.Exists(path)) {
                Directory.CreateDirectory(path);
            }
        }

        private static ServerRepository GetServerRepository() {
            return new ServerRepository(GetRepositoryPath());
        }
        
        private static IFileSystem GetFileSystem() {
            string path = GetEditorPath();

            // If we find a solution file then use the vs implementation so only get a subset
            // of the files (ones included in the project)
            if (Directory.EnumerateFiles(path, "*.sln").Any()) {
                return new SolutionFileSystem(path);
            }

            return new PhysicalFileSystem(path);
        }
    }
}

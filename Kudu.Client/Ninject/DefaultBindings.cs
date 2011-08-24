using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.SourceControl;
using Ninject;
using Ninject.Activation;
using Ninject.Modules;

namespace Kudu.Client {
    public class DefaultBindings : NinjectModule {
        public override void Load() {
            Bind<ISiteConfiguration>().To<SiteConfiguration>();
            Bind<IFileSystem>().ToMethod(context => GetFileSystem(context));
            Bind<IRepository>().ToMethod(context => GetRepository(context));
            Bind<IRepositoryManager>().ToMethod(context => GetRepositoryManager(context));
            Bind<IDeploymentManager>().ToMethod(context => GetDeploymentManager(context));
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

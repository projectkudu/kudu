using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SignalR.Infrastructure;
using Ninject;
using Ninject.Activation;
using Ninject.Modules;

namespace Kudu.SignalR {
    public class DefaultBindings : NinjectModule {
        public override void Load() {
            Bind<ISiteConfiguration>().To<SiteConfiguration>();
            Bind<IRepository>().ToMethod(context => GetRepository(context));
            Bind<IDeploymentManager>().ToMethod(context => GetDeploymentManager(context));
        }

        private static IRepository GetRepository(IContext context) {
            var siteConfiguration = context.Kernel.Get<ISiteConfiguration>();
            return siteConfiguration.Repository;
        }

        private static IDeploymentManager GetDeploymentManager(IContext context) {
            var siteConfiguration = context.Kernel.Get<ISiteConfiguration>();
            return siteConfiguration.DeploymentManager;
        }

    }
}

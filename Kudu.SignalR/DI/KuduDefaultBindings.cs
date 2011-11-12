using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SignalR.Infrastructure;
using Kudu.SignalR.Models;
using SignalR.Infrastructure;

namespace Kudu.SignalR
{
    public static class KuduDefaultBindings 
    {
        public static void Initialize()
        {
            DependencyResolver.Register(typeof(ISiteConfiguration), GetSiteConfiguration);
            DependencyResolver.Register(typeof(IRepository), GetRepository);
            DependencyResolver.Register(typeof(IDeploymentManager), GetDeploymentManager);            
        }

        private static ISiteConfiguration GetSiteConfiguration()
        {
            var application = DependencyResolver.Resolve<IApplication>();
            var credentialProvider = DependencyResolver.Resolve<ICredentialProvider>();
            return new SiteConfiguration(application, credentialProvider);
        }

        private static IRepository GetRepository()
        {
            var siteConfiguration = DependencyResolver.Resolve<ISiteConfiguration>();
            return siteConfiguration.Repository;
        }

        private static IDeploymentManager GetDeploymentManager()
        {
            var siteConfiguration = DependencyResolver.Resolve<ISiteConfiguration>();
            return siteConfiguration.DeploymentManager;
        }

    }
}

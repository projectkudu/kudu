using System;
using System.IO;
using Kudu.Client.Deployment;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;

namespace Kudu.FunctionalTests.Infrastructure
{
    public class ApplicationManager : IDisposable
    {
        private readonly ISiteManager _siteManager;
        private readonly Site _site;
        private readonly string _appName;

        private ApplicationManager(ISiteManager siteManager, Site site, string appName)
        {
            _siteManager = siteManager;
            _site = site;
            _appName = appName;
        }

        public string SiteUrl
        {
            get;
            private set;
        }

        public string RepositoryPath
        {
            get
            {
                return Path.Combine(PathHelper.SitesPath, _appName, @"live\repository");
            }
        }        

        public RemoteDeploymentManager DeploymentManager
        {
            get;
            private set;
        }

        public string GitUrl
        {
            get
            {
                return GetCloneUrl(_site, RepositoryType.Git);
            }
        }

        public string HgUrl
        {
            get
            {
                return GetCloneUrl(_site, RepositoryType.Mercurial);
            }
        }

        void IDisposable.Dispose()
        {
            siteManager.DeleteSite(_appName);
        }

        public static ApplicationManager CreateApplication(string applicationName)
        {
            var pathResolver = new DefaultPathResolver(PathHelper.ServiceSitePath, PathHelper.SitesPath);
            var siteManager = new SiteManager(pathResolver);
            Site site = siteManager.CreateSite(applicationName);

            return new ApplicationManager(siteManager, site, applicationName)
            {
                SiteUrl = site.SiteUrl,
                DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "deploy")
            };
        }

        private string GetCloneUrl(Site site, RepositoryType type)
        {
            return site.ServiceUrl + (type == RepositoryType.Git ? "git" : "hg");
        }
    }
}

using System;
using System.IO;
using Kudu.Client.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;
using System.Diagnostics;

namespace Kudu.FunctionalTests.Infrastructure
{
    public class ApplicationManager
    {
        private readonly ISiteManager _siteManager;
        private readonly Site _site;
        private readonly string _appName;
        private readonly string _path;

        private ApplicationManager(ISiteManager siteManager, Site site, string appName, string path)
        {
            _siteManager = siteManager;
            _site = site;
            _appName = appName;
            _path = path;
        }

        public string SiteUrl
        {
            get;
            private set;
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

        public static ApplicationManager CreateApplication(string applicationName)
        {
            var pathResolver = new DefaultPathResolver(PathHelper.ServiceSitePath, PathHelper.SitesPath);
            var siteManager = GetSiteManager(pathResolver);

            try
            {
                siteManager.DeleteSite(applicationName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            Site site = siteManager.CreateSite(applicationName);

            return new ApplicationManager(siteManager, site, applicationName, pathResolver.GetApplicationPath(applicationName))
            {
                SiteUrl = site.SiteUrl,
                DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "deploy")
            };
        }

        private string GetCloneUrl(Site site, RepositoryType type)
        {
            return site.ServiceUrl + (type == RepositoryType.Git ? "git" : "hg");
        }

        private static SiteManager GetSiteManager(DefaultPathResolver pathResolver)
        {
            return new SiteManager(pathResolver);
        }
    }
}

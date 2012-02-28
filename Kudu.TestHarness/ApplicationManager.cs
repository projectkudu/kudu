using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using Kudu.Client.Deployment;
using Kudu.Client.Editor;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;

namespace Kudu.TestHarness
{
    public class ApplicationManager
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

        public string ServiceUrl
        {
            get;
            private set;
        }

        public RemoteDeploymentManager DeploymentManager
        {
            get;
            private set;
        }

        public RemoteProjectSystem ProjectSystem
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

        private void Delete()
        {
            _siteManager.DeleteSite(_appName);
        }

        public static void Run(string applicationName, Action<ApplicationManager> action)
        {
            var appManager = CreateApplication(applicationName);
            var dumpPath = Path.Combine(PathHelper.TestResultsPath, applicationName + ".zip");
            try
            {
                action(appManager);

                KuduUtils.DownloadDump(appManager.ServiceUrl, dumpPath);

                appManager.Delete();
            }
            catch (Exception ex)
            {
                KuduUtils.DownloadDump(appManager.ServiceUrl, dumpPath);

                Debug.WriteLine(ex.Message);
                throw;
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
            catch (System.ServiceModel.EndpointNotFoundException)
            {

            }

            Site site = siteManager.CreateSite(applicationName);

            return new ApplicationManager(siteManager, site, applicationName)
            {
                SiteUrl = site.SiteUrl,
                ServiceUrl = site.ServiceUrl,
                DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "deployments"),
                ProjectSystem = new RemoteProjectSystem(site.ServiceUrl + "live/files")
            };
        }

        private string GetCloneUrl(Site site, Kudu.Core.SourceControl.RepositoryType type)
        {
            return site.ServiceUrl + (type == RepositoryType.Git ? "git" : "hg");
        }

        private static SiteManager GetSiteManager(DefaultPathResolver pathResolver)
        {
            return new SiteManager(pathResolver);
        }
    }
}

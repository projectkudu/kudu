using System;
using System.Diagnostics;
using System.IO;
using Kudu.Client.Deployment;
using Kudu.Client.Editor;
using Kudu.Client.SourceControl;
using Kudu.SiteManagement;

namespace Kudu.TestHarness
{
    public class ApplicationManager
    {
        private readonly ISiteManager _siteManager;
        private readonly Site _site;
        private readonly string _appName;

        private ApplicationManager(ISiteManager siteManager, Site site, string appName, string gitUrl)
        {
            _siteManager = siteManager;
            _site = site;
            _appName = appName;
            GitUrl = gitUrl;
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
            get;
            private set;
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

            string gitUrl = null;
            try
            {
                var repositoryManager = new RemoteRepositoryManager(site.ServiceUrl + "live/scm");
                var repositoryInfo = repositoryManager.GetRepositoryInfo().Result;
                gitUrl = repositoryInfo.GitUrl.ToString();
            }
            catch
            {
                gitUrl = site.ServiceUrl + "git";
            }

            return new ApplicationManager(siteManager, site, applicationName, gitUrl)
            {
                SiteUrl = site.SiteUrl,
                ServiceUrl = site.ServiceUrl,
                DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "deployments"),
                ProjectSystem = new RemoteProjectSystem(site.ServiceUrl + "live/files")
            };
        }

        private static SiteManager GetSiteManager(DefaultPathResolver pathResolver)
        {
            return new SiteManager(pathResolver);
        }
    }
}

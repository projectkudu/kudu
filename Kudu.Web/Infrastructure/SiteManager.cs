using System;
using System.IO;
using System.Web;
using Kudu.Core.Infrastructure;
using Kudu.Web.Models;
using IIS = Microsoft.Web.Administration;

namespace Kudu.Web.Infrastructure {
    public class SiteManager : ISiteManager {
        public Site CreateSite(string applicationName) {
            var iis = new IIS.ServerManager();

            var kuduAppPool = EnsureAppPool(iis);
            string liveSiteName = GetLiveSite(applicationName);

            try {
                // Create the services site
                var serviceSite = GetServiceSite(iis, kuduAppPool);

                // Get the port of the site
                int servicePort = serviceSite.Bindings[0].EndPoint.Port;
                var serviceApp = serviceSite.Applications.Add("/" + applicationName, PathHelper.ServiceSitePath);
                serviceApp.ApplicationPoolName = kuduAppPool.Name;

                // Get the path to the website
                string siteRoot = Path.Combine(PathHelper.GetApplicationPath(applicationName), "wwwroot");
                int sitePort = GetRandomPort();
                var site = iis.Sites.Add(liveSiteName, siteRoot, sitePort);
                site.ApplicationDefaults.ApplicationPoolName = kuduAppPool.Name;

                iis.CommitChanges();

                return new Site {
                    SiteName = liveSiteName,
                    ServiceUrl = String.Format("http://localhost:{0}/{1}/", servicePort, applicationName),
                    SiteUrl = String.Format("http://localhost:{0}/", sitePort),
                };
            }
            catch {
                DeleteSite(applicationName);
                throw;
            }
        }

        public bool TryCreateDeveloperSite(string siteName, out string siteUrl) {
            var iis = new IIS.ServerManager();

            string devSiteName = GetDevSite(siteName);

            IIS.Site site = iis.Sites[devSiteName];
            if (site == null) {
                var kuduAppPool = EnsureAppPool(iis);

                // Get the path to the website
                string siteRoot = Path.Combine(PathHelper.GetDeveloperApplicationPath(siteName), "wwwroot");
                FileSystemHelpers.EnsureDirectory(siteRoot);

                int sitePort = GetRandomPort();
                site = iis.Sites.Add(devSiteName, siteRoot, sitePort);
                site.ApplicationDefaults.ApplicationPoolName = kuduAppPool.Name;

                iis.CommitChanges();
                siteUrl = String.Format("http://localhost:{0}/", sitePort);
                return true;
            }

            siteUrl = null;
            return false;
        }

        private static IIS.ApplicationPool EnsureAppPool(IIS.ServerManager iis) {
            var kuduAppPool = iis.ApplicationPools["kudu"];
            if (kuduAppPool == null) {
                iis.ApplicationPools.Add("kudu");
                iis.CommitChanges();
                kuduAppPool = iis.ApplicationPools["kudu"];
                kuduAppPool.Enable32BitAppOnWin64 = true;
                kuduAppPool.ManagedPipelineMode = IIS.ManagedPipelineMode.Integrated;
                kuduAppPool.ManagedRuntimeVersion = "v4.0";
                kuduAppPool.AutoStart = true;
            }

            return kuduAppPool;
        }

        private IIS.Site GetServiceSite(IIS.ServerManager iis, IIS.ApplicationPool appPool) {
            var site = iis.Sites["kudu_services"];
            if (site == null) {
                site = iis.Sites.Add("kudu_services", PathHelper.ServiceSitePath, GetRandomPort());
                site.ApplicationDefaults.ApplicationPoolName = appPool.Name;
            }
            return site;
        }

        private int GetRandomPort() {
            // TODO: Ensure the port is unused
            return new Random((int)DateTime.Now.Ticks).Next(1025, 65535);
        }

        public void DeleteSite(string applicationName) {
            var iis = new IIS.ServerManager();

            DeleteSite(iis, GetLiveSite(applicationName));
            DeleteSite(iis, GetDevSite(applicationName));
            // Delete the services application
            var servicesSite = iis.Sites["kudu_services"];
            if (servicesSite != null) {
                var app = servicesSite.Applications["/" + applicationName];
                if (app != null) {
                    string appPath = PathHelper.GetApplicationPath(applicationName);
                    DeleteSafe(appPath);
                    servicesSite.Applications.Remove(app);
                }
            }

            iis.CommitChanges();
        }

        public void SetDeveloperSiteWebRoot(string applicationName, string projectPath) {
            var iis = new IIS.ServerManager();
            string siteName = GetDevSite(applicationName);

            IIS.Site site = iis.Sites[siteName];
            if (site != null) {
                string devSitePath = PathHelper.GetDeveloperApplicationPath(applicationName);
                string path = Path.Combine(devSitePath, "wwwroot", Path.GetDirectoryName(projectPath));
                site.Applications[0].VirtualDirectories[0].PhysicalPath = path;

                iis.CommitChanges();
            }
        }
        
        private void DeleteSite(IIS.ServerManager iis, string siteName) {
            var site = iis.Sites[siteName];
            if (site != null) {
                site.Stop();
                string physicalPath = site.Applications[0].VirtualDirectories[0].PhysicalPath;
                DeleteSafe(physicalPath);
                iis.Sites.Remove(site);
            }
        }

        private string GetDevSite(string applicationName) {
            return "kudu_dev_" + applicationName;
        }

        private string GetLiveSite(string applicationName) {
            return "kudu_" + applicationName;
        }

        private static void DeleteSafe(string physicalPath) {
            if (!Directory.Exists(physicalPath)) {
                return;
            }

            FileSystemHelpers.DeleteDirectorySafe(physicalPath);
        }        
    }
}
using System;
using System.IO;
using System.Web;
using Ionic.Zip;
using Kudu.Web.Models;
using IIS = Microsoft.Web.Administration;
using System.Threading;

namespace Kudu.Web.Infrastructure {
    public class SiteManager : ISiteManager {
        private const string ServiceWebZip = "Kudu.Web.Infrastructure.Site.serviceweb.zip";

        public Site CreateSite(string siteName) {
            var iis = new IIS.ServerManager();

            var kuduAppPool = EnsureAppPool(iis);
            string liveSiteName = "kudu_" + siteName;

            try {
                // Create the services site
                var serviceSite = EnsureServiceSite(iis, kuduAppPool);

                // Get the physical path of the services site
                string serviceSiteRoot = EnsureServiceSite();

                // Get the port of the site
                int servicePort = serviceSite.Bindings[0].EndPoint.Port;
                var serviceApp = serviceSite.Applications.Add("/" + siteName, serviceSiteRoot);
                serviceApp.ApplicationPoolName = kuduAppPool.Name;

                // Get the path to the website
                string siteRoot = Path.Combine(serviceSiteRoot, @"App_Data", "_root", siteName, "wwwroot");
                int sitePort = GetRandomPort();
                var site = iis.Sites.Add(liveSiteName, siteRoot, sitePort);
                site.ApplicationDefaults.ApplicationPoolName = kuduAppPool.Name;

                iis.CommitChanges();

                return new Site {
                    SiteName = liveSiteName,
                    ServiceUrl = String.Format("http://localhost:{0}/{1}/", servicePort, siteName),
                    SiteUrl = String.Format("http://localhost:{0}/", sitePort),
                };
            }
            catch {
                DeleteSite(liveSiteName, siteName);
                throw;
            }
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

        private IIS.Site EnsureServiceSite(IIS.ServerManager iis, IIS.ApplicationPool appPool) {
            var site = iis.Sites["kudu_services"];
            if (site == null) {
                string path = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "sites");
                site = iis.Sites.Add("kudu_services", path, GetRandomPort());
                site.ApplicationDefaults.ApplicationPoolName = appPool.Name;
            }
            return site;
        }

        private string EnsureServiceSite() {
            string root = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", "sites");
            string destPath = Path.Combine(root, "wwwroot");

            try {
                using (var stream = typeof(SiteManager).Assembly.GetManifestResourceStream(ServiceWebZip)) {
                    using (var file = ZipFile.Read(stream)) {
                        file.ExtractAll(destPath, ExtractExistingFileAction.OverwriteSilently);
                    }
                }
            }
            catch (UnauthorizedAccessException) {
                // File might be locked
            }

            return destPath;
        }

        private int GetRandomPort() {
            // TODO: Ensure the port is unused
            return new Random((int)DateTime.Now.Ticks).Next(1025, 65535);
        }

        public void DeleteSite(string siteName, string applicationName) {
            var iis = new IIS.ServerManager();
            var site = iis.Sites[siteName];
            if (site != null) {
                string physicalPath = site.Applications[0].VirtualDirectories[0].PhysicalPath;
                DeleteSafe(physicalPath);
                iis.Sites.Remove(site);

                // Delete the services application
                var servicesSite = iis.Sites["kudu_services"];
                if (servicesSite != null) {
                    var app = servicesSite.Applications["/" + applicationName];
                    if (app != null) {
                        servicesSite.Applications.Remove(app);
                    }
                }
            }
            iis.CommitChanges();
        }

        private static void DeleteSafe(string physicalPath) {
            if (!Directory.Exists(physicalPath)) {
                return;
            }

            try {
                Directory.Delete(physicalPath, recursive: true);
            }
            catch {

            }
        }
    }
}
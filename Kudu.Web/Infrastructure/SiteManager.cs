using System;
using System.IO;
using System.Threading;
using System.Web;
using Ionic.Zip;
using Kudu.Web.Models;
using IIS = Microsoft.Web.Administration;

namespace Kudu.Web.Infrastructure {
    public class SiteManager : ISiteManager {
        private const string ServiceWebZip = "Kudu.Web.Infrastructure.Site.serviceweb.zip";

        public Site CreateSite(string siteName) {
            var iis = new IIS.ServerManager();

            var kuduAppPool = EnsureAppPool(iis);
            string prefix = "kudu_" + siteName;
            string serviceSiteName = prefix + "_service";
            string liveSiteName = prefix + "_site";

            try {
                string serviceSiteRoot = GetServiceSitePath(siteName);
                string siteRoot = Path.Combine(serviceSiteRoot, @"App_Data", "_root", "wwwroot");

                int servicePort = GetRandomPort();
                var serviceSite = iis.Sites.Add(serviceSiteName, serviceSiteRoot, servicePort);
                int sitePort = GetRandomPort();
                var site = iis.Sites.Add(liveSiteName, siteRoot, sitePort);

                serviceSite.ApplicationDefaults.ApplicationPoolName = kuduAppPool.Name;
                site.ApplicationDefaults.ApplicationPoolName = kuduAppPool.Name;

                iis.CommitChanges();

                return new Site {
                    SiteName = liveSiteName,
                    ServiceName = serviceSiteName,
                    ServiceUrl = String.Format("http://localhost:{0}/", servicePort),
                    SiteUrl = String.Format("http://localhost:{0}/", sitePort),
                };
            }
            catch {
                DeleteSite(siteName);
                DeleteSite(serviceSiteName);
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

        private string GetServiceSitePath(string siteName) {
            string root = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", siteName);
            string destPath = Path.Combine(root, "wwwroot");

            using (var stream = typeof(SiteManager).Assembly.GetManifestResourceStream(ServiceWebZip)) {
                using (var file = ZipFile.Read(stream)) {
                    file.ExtractAll(destPath, ExtractExistingFileAction.OverwriteSilently);
                }
            }

            return destPath;
        }

        private int GetRandomPort() {
            // TODO: Ensure the port is unused
            return new Random((int)DateTime.Now.Ticks).Next(1025, 65535);
        }

        public void DeleteSite(string siteName) {
            var iis = new IIS.ServerManager();
            var site = iis.Sites[siteName];
            if (site != null) {
                string physicalPath = site.Applications[0].VirtualDirectories[0].PhysicalPath;
                DeleteSafe(physicalPath);
                iis.Sites.Remove(site);
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
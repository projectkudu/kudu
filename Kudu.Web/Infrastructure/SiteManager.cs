using System;
using System.IO;
using System.Web;
using Ionic.Zip;
using Kudu.Web.Models;
using IIS = Microsoft.Web.Administration;

namespace Kudu.Web.Infrastructure {
    public class SiteManager : ISiteManager {
        private const string ResourceZip = "Kudu.Web.Infrastructure.Site.serviceweb.zip";

        public Site CreateSite(string name) {
            var iis = new IIS.ServerManager();

            var kuduAppPool = EnsureAppPool(iis);

            string prefix = "kudu_" + name;
            string siteId = Guid.NewGuid().ToString("d").Substring(0, 4);
            string serviceSiteName = prefix + "_service_" + siteId;
            string siteName = prefix + "_site_" + siteId;

            string serviceSiteRoot = GetServiceSitePath(siteId);
            string siteRoot = Path.Combine(serviceSiteRoot, @"App_Data", "_root", "wwwroot");

            int servicePort = GetRandomPort();
            var serviceSite = iis.Sites.Add(serviceSiteName, serviceSiteRoot, servicePort);
            int sitePort = GetRandomPort();
            var site = iis.Sites.Add(siteName, siteRoot, sitePort);

            serviceSite.ApplicationDefaults.ApplicationPoolName = kuduAppPool.Name;
            site.ApplicationDefaults.ApplicationPoolName = kuduAppPool.Name;

            iis.CommitChanges();

            return new Site {
                SiteName = siteName,
                ServiceName = serviceSiteName,
                ServiceUrl = String.Format("http://localhost:{0}/", servicePort),
                SiteUrl = String.Format("http://localhost:{0}/", sitePort),
            };
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
                kuduAppPool.SetAttributeValue("Identity", "LocalSystem");
            }
            return kuduAppPool;
        }

        private string GetServiceSitePath(string siteId) {            
            string root = Path.Combine(HttpRuntime.AppDomainAppPath, "App_Data", siteId);
            string destPath = Path.Combine(root, "wwwroot");

            using (var stream = typeof(SiteManager).Assembly.GetManifestResourceStream(ResourceZip)) {
                using (var file = ZipFile.Read(stream)) {
                    file.ExtractAll(destPath);
                }
            }

            return destPath;
        }

        private int GetRandomPort() {
            // TODO: Ensure the port is unused
            return new Random((int)DateTime.Now.Ticks).Next(1025, 65535);
        }

        public void DeleteSite(Application app) {
            var iis = new IIS.ServerManager();
            var serviceSite = iis.Sites[app.ServiceName];
            if (serviceSite != null) {
                iis.Sites.Remove(serviceSite);
            }
            var site = iis.Sites[app.SiteName];
            if (site != null) {
                iis.Sites.Remove(site);
            }
            iis.CommitChanges();
        }
    }
}
using System;
using System.IO;
using Kudu.Core.Infrastructure;
using Kudu.Web.Models;
using IIS = Microsoft.Web.Administration;
using System.Threading;

namespace Kudu.Web.Infrastructure
{
    public class SiteManager : ISiteManager
    {
        private const string KuduAppPoolName = "kudu";

        public Site CreateSite(string applicationName)
        {
            var iis = new IIS.ServerManager();

            try
            {
                // Create the service site for this site
                string serviceSiteName = GetServiceSite(applicationName);
                int serviceSitePort = CreateSite(iis, serviceSiteName, PathHelper.ServiceSitePath);

                // Create the main site
                string siteName = GetLiveSite(applicationName);
                string siteRoot = PathHelper.GetApplicationPath(applicationName);
                string webRoot = Path.Combine(siteRoot, "wwwroot");
                int sitePort = CreateSite(iis, siteName, webRoot);

                // Commit the changes to iis
                iis.CommitChanges();

                // Map a path called app to the site root under the service site
                MapServiceSitePath(iis, applicationName, "_app", siteRoot);

                return new Site
                {
                    SiteName = siteName,
                    ServiceUrl = String.Format("http://localhost:{0}/", serviceSitePort),
                    SiteUrl = String.Format("http://localhost:{0}/", sitePort),
                };
            }
            catch
            {
                DeleteSite(applicationName);
                throw;
            }
        }

        public bool TryCreateDeveloperSite(string applicationName, out string siteUrl)
        {
            var iis = new IIS.ServerManager();

            string devSiteName = GetDevSite(applicationName);

            IIS.Site site = iis.Sites[devSiteName];
            if (site == null)
            {
                // Get the path to the dev site
                string siteRoot = PathHelper.GetDeveloperApplicationPath(applicationName);
                string webRoot = Path.Combine(siteRoot, "wwwroot");
                int sitePort = CreateSite(iis, devSiteName, webRoot);

                // Ensure the directory is created
                FileSystemHelpers.EnsureDirectory(webRoot);

                iis.CommitChanges();

                // Map a path called app to the site root under the service site
                MapServiceSitePath(iis, applicationName, "_devapp", siteRoot, restartSite: true);

                siteUrl = String.Format("http://localhost:{0}/", sitePort);
                return true;
            }

            siteUrl = null;
            return false;
        }

        public void DeleteSite(string applicationName)
        {
            var iis = new IIS.ServerManager();

            DeleteSite(iis, GetLiveSite(applicationName));
            DeleteSite(iis, GetDevSite(applicationName));
            // Don't delete the physical files for the service site
            DeleteSite(iis, GetServiceSite(applicationName), deletePhysicalFiles: false);

            iis.CommitChanges();
        }

        public void SetDeveloperSiteWebRoot(string applicationName, string projectPath)
        {
            var iis = new IIS.ServerManager();
            string siteName = GetDevSite(applicationName);

            IIS.Site site = iis.Sites[siteName];
            if (site != null)
            {
                string devSitePath = PathHelper.GetDeveloperApplicationPath(applicationName);
                string path = Path.Combine(devSitePath, "wwwroot", Path.GetDirectoryName(projectPath));
                site.Applications[0].VirtualDirectories[0].PhysicalPath = path;

                iis.CommitChanges();
            }
        }

        private static void MapServiceSitePath(IIS.ServerManager iis, string applicationName, string path, string siteRoot, bool restartSite = false)
        {
            string serviceSiteName = GetServiceSite(applicationName);

            // Get the service site
            IIS.Site site = iis.Sites[serviceSiteName];
            if (site == null)
            {
                throw new InvalidOperationException("Could not retrieve service site");
            }


            // Map the path to the live site in the service site
            site.Applications.Add("/" + path, siteRoot);

            iis.CommitChanges();

            if (restartSite)
            {
                site.Stop();
                Thread.Sleep(500);
                site.Start();
            }
        }

        private static IIS.ObjectState GetState(IIS.Site site)
        {
            try
            {
                return site.State;
            }
            catch
            {
                return IIS.ObjectState.Unknown;
            }
        }

        private static IIS.ApplicationPool EnsureKuduAppPool(IIS.ServerManager iis)
        {
            var kuduAppPool = iis.ApplicationPools[KuduAppPoolName];
            if (kuduAppPool == null)
            {
                iis.ApplicationPools.Add(KuduAppPoolName);
                iis.CommitChanges();
                kuduAppPool = iis.ApplicationPools[KuduAppPoolName];
                kuduAppPool.Enable32BitAppOnWin64 = true;
                kuduAppPool.ManagedPipelineMode = IIS.ManagedPipelineMode.Integrated;
                kuduAppPool.ManagedRuntimeVersion = "v4.0";
                kuduAppPool.AutoStart = true;
            }

            return kuduAppPool;
        }

        private int GetRandomPort()
        {
            // TODO: Ensure the port is unused
            return new Random((int)DateTime.Now.Ticks).Next(1025, 65535);
        }

        private int CreateSite(IIS.ServerManager iis, string siteName, string siteRoot)
        {
            EnsureKuduAppPool(iis);

            int sitePort = GetRandomPort();
            var site = iis.Sites.Add(siteName, siteRoot, sitePort);
            site.ApplicationDefaults.ApplicationPoolName = KuduAppPoolName;

            return sitePort;
        }

        private void DeleteSite(IIS.ServerManager iis, string siteName, bool deletePhysicalFiles = true)
        {
            var site = iis.Sites[siteName];
            if (site != null)
            {
                site.Stop();
                if (deletePhysicalFiles)
                {
                    string physicalPath = site.Applications[0].VirtualDirectories[0].PhysicalPath;
                    DeleteSafe(physicalPath);
                }
                iis.Sites.Remove(site);
            }
        }

        private static string GetDevSite(string applicationName)
        {
            return "kudu_dev_" + applicationName;
        }

        private static string GetLiveSite(string applicationName)
        {
            return "kudu_" + applicationName;
        }

        private static string GetServiceSite(string applicationName)
        {
            return "kudu_service_" + applicationName;
        }

        private static void DeleteSafe(string physicalPath)
        {
            if (!Directory.Exists(physicalPath))
            {
                return;
            }

            FileSystemHelpers.DeleteDirectorySafe(physicalPath);
        }
    }
}
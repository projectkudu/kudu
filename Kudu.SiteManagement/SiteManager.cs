using System;
using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using System.Threading;
using Kudu.Core.Infrastructure;
using IIS = Microsoft.Web.Administration;

namespace Kudu.SiteManagement
{
    public class SiteManager : ISiteManager
    {
        private static Random portNumberGenRnd = new Random((int)DateTime.Now.Ticks);

        private readonly IPathResolver _pathResolver;
        private readonly bool _traceFailedRequests;
        private readonly string _logPath;

        public SiteManager(IPathResolver pathResolver)
            : this(pathResolver, traceFailedRequests: false, logPath: null)
        {
        }

        public SiteManager(IPathResolver pathResolver, bool traceFailedRequests, string logPath)
        {
            _logPath = logPath;
            _pathResolver = pathResolver;
            _traceFailedRequests = traceFailedRequests;
        }

        public Site CreateSite(string applicationName)
        {
            var iis = new IIS.ServerManager();

            try
            {
                // Create the service site for this site
                string serviceSiteName = GetServiceSite(applicationName);
                int serviceSitePort = CreateSite(iis, applicationName, serviceSiteName, _pathResolver.ServiceSitePath);

                // Create the main site
                string siteName = GetLiveSite(applicationName);
                string siteRoot = _pathResolver.GetLiveSitePath(applicationName);
                string webRoot = Path.Combine(siteRoot, Constants.WebRoot);

                FileSystemHelpers.EnsureDirectory(webRoot);
                File.WriteAllText(Path.Combine(webRoot, "index.html"), @"<html> 
<head>
<title>The web site is under construction</title>
<style type=""text/css"">
 BODY { color: #444444; background-color: #E5F2FF; font-family: verdana; margin: 0px; text-align: center; margin-top: 100px; }
 H1 { font-size: 16pt; margin-bottom: 4px; }
</style>
</head>
<body>
<h1>The web site is under construction</h1><br/>
</body> 
</html>");

                int sitePort = CreateSite(iis, applicationName, siteName, webRoot);

                // Map a path called app to the site root under the service site
                MapServiceSitePath(iis, applicationName, Constants.MappedLiveSite, siteRoot);

                // Commit the changes to iis
                iis.CommitChanges();

                // Give IIS some time to create the site and map the path
                // REVIEW: Should we poll the site's state?
                Thread.Sleep(1000);

                return new Site
                {
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
                string siteRoot = _pathResolver.GetDeveloperApplicationPath(applicationName);
                string webRoot = Path.Combine(siteRoot, Constants.WebRoot);
                int sitePort = CreateSite(iis, applicationName, devSiteName, webRoot);

                // Ensure the directory is created
                FileSystemHelpers.EnsureDirectory(webRoot);

                // Map a path called app to the site root under the service site
                MapServiceSitePath(iis, applicationName, Constants.MappedDevSite, siteRoot);

                iis.CommitChanges();


                siteUrl = String.Format("http://localhost:{0}/", sitePort);
                return true;
            }

            siteUrl = null;
            return false;
        }

        public void DeleteSite(string applicationName)
        {
            var iis = new IIS.ServerManager();

            // Get the app pool for this application
            string appPoolName = GetAppPool(applicationName);
            IIS.ApplicationPool kuduPool = iis.ApplicationPools[appPoolName];

            // Make sure the acls are gone
            RemoveAcls(applicationName, appPoolName);

            if (kuduPool == null)
            {
                // If there's no app pool then do nothing
                return;
            }

            DeleteSite(iis, GetLiveSite(applicationName));
            DeleteSite(iis, GetDevSite(applicationName));
            // Don't delete the physical files for the service site
            DeleteSite(iis, GetServiceSite(applicationName), deletePhysicalFiles: false);

            iis.CommitChanges();

            string appPath = _pathResolver.GetApplicationPath(applicationName);
            var sitePath = _pathResolver.GetLiveSitePath(applicationName);
            var devPath = _pathResolver.GetDeveloperApplicationPath(applicationName);

            try
            {
                kuduPool.StopAndWait();

                DeleteSafe(sitePath);
                DeleteSafe(devPath);
                DeleteSafe(appPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
            }
            finally
            {
                // Remove the app pool and commit changes
                iis.ApplicationPools.Remove(iis.ApplicationPools[appPoolName]);
                iis.CommitChanges();
            }
        }

        public void SetDeveloperSiteWebRoot(string applicationName, string siteRoot)
        {
            var iis = new IIS.ServerManager();
            string siteName = GetDevSite(applicationName);

            IIS.Site site = iis.Sites[siteName];
            if (site != null)
            {
                string devSitePath = _pathResolver.GetDeveloperApplicationPath(applicationName);
                string webRoot = Path.Combine(devSitePath, Constants.WebRoot, siteRoot);

                // Change the web root
                site.Applications[0].VirtualDirectories[0].PhysicalPath = webRoot;

                iis.CommitChanges();

                Thread.Sleep(1000);
            }
        }

        private static void MapServiceSitePath(IIS.ServerManager iis, string applicationName, string path, string siteRoot)
        {
            string serviceSiteName = GetServiceSite(applicationName);

            // Get the service site
            IIS.Site site = iis.Sites[serviceSiteName];
            if (site == null)
            {
                throw new InvalidOperationException("Could not retrieve service site");
            }

            // Map the path to the live site in the service site
            site.Applications.Add(path, siteRoot);
        }

        private IIS.ApplicationPool EnsureAppPool(IIS.ServerManager iis, string appName)
        {
            string appPoolName = GetAppPool(appName);
            var kuduAppPool = iis.ApplicationPools[appPoolName];
            if (kuduAppPool == null)
            {
                iis.ApplicationPools.Add(appPoolName);
                iis.CommitChanges();
                kuduAppPool = iis.ApplicationPools[appPoolName];
                kuduAppPool.Enable32BitAppOnWin64 = true;
                kuduAppPool.ManagedPipelineMode = IIS.ManagedPipelineMode.Integrated;
                kuduAppPool.ManagedRuntimeVersion = "v4.0";
                kuduAppPool.AutoStart = true;
                kuduAppPool.ProcessModel.LoadUserProfile = false;
                kuduAppPool.WaitForState(IIS.ObjectState.Started);

                SetupAcls(appName, appPoolName);
            }

            return kuduAppPool;
        }

        private void RemoveAcls(string appName, string appPoolName)
        {
            // Setup Acls for this user
            var icacls = new Executable(@"C:\Windows\System32\icacls.exe", Directory.GetCurrentDirectory());

            string applicationPath = _pathResolver.GetApplicationPath(appName);

            try
            {
                // Give full control to the app folder (we can make it minimal later)
                icacls.Execute(@"""{0}\"" /remove ""IIS AppPool\{1}""", applicationPath, appPoolName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            try
            {
                icacls.Execute(@"""{0}"" /remove ""IIS AppPool\{1}""", _pathResolver.ServiceSitePath, appPoolName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            try
            {
                // Give full control to the temp folder
                string windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                icacls.Execute(@"""{0}"" /remove ""IIS AppPool\{1}""", windowsTemp, appPoolName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private void SetupAcls(string appName, string appPoolName)
        {
            // Setup Acls for this user
            var icacls = new Executable(@"C:\Windows\System32\icacls.exe", Directory.GetCurrentDirectory());

            // Make sure the application path exists
            string applicationPath = _pathResolver.GetApplicationPath(appName);
            Directory.CreateDirectory(applicationPath);

            try
            {
                // Give full control to the app folder (we can make it minimal later)
                icacls.Execute(@"""{0}"" /grant:r ""IIS AppPool\{1}:(OI)(CI)(F)"" /C /Q /T", applicationPath, appPoolName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            try
            {
                icacls.Execute(@"""{0}"" /grant:r ""IIS AppPool\{1}:(OI)(CI)(RX)"" /C /Q /T", _pathResolver.ServiceSitePath, appPoolName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }

            try
            {
                // Give full control to the temp folder
                string windowsTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                icacls.Execute(@"""{0}"" /grant:r ""IIS AppPool\{1}:(OI)(CI)(F)"" /C /Q /T", windowsTemp, appPoolName);
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.Message);
            }
        }

        private int GetRandomPort(IIS.ServerManager iis)
        {
            int randomPort = portNumberGenRnd.Next(1025, 65535);
            while (!IsAvailable(randomPort, iis))
            {
                randomPort = portNumberGenRnd.Next(1025, 65535);
            }

            return randomPort;
        }

        private bool IsAvailable(int port, IIS.ServerManager iis)
        {
            var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            foreach (var connectionInfo in tcpConnections)
            {
                if (connectionInfo.LocalEndPoint.Port == port)
                {
                    return false;
                }
            }

            foreach (var iisSite in iis.Sites)
            {
                foreach (var binding in iisSite.Bindings)
                {
                    if (binding.EndPoint != null && binding.EndPoint.Port == port)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private int CreateSite(IIS.ServerManager iis, string applicationName, string siteName, string siteRoot)
        {
            var pool = EnsureAppPool(iis, applicationName);

            int sitePort = GetRandomPort(iis);
            var site = iis.Sites.Add(siteName, siteRoot, sitePort);
            site.ApplicationDefaults.ApplicationPoolName = pool.Name;

            if (_traceFailedRequests)
            {
                site.TraceFailedRequestsLogging.Enabled = true;
                string path = Path.Combine(_logPath, applicationName, "Logs");
                Directory.CreateDirectory(path);
                site.TraceFailedRequestsLogging.Directory = path;
            }

            return sitePort;
        }

        private void DeleteSite(IIS.ServerManager iis, string siteName, bool deletePhysicalFiles = true)
        {
            var site = iis.Sites[siteName];
            if (site != null)
            {
                site.StopAndWait();
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

        private static string GetAppPool(string applicationName)
        {
            return applicationName;
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
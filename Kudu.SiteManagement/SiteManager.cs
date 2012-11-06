using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        private readonly ISettingsResolver _settingsResolver;

        public SiteManager(IPathResolver pathResolver, ISettingsResolver settingsResolver)
            : this(pathResolver, traceFailedRequests: false, logPath: null, settingsResolver: settingsResolver)
        {
        }

        public SiteManager(IPathResolver pathResolver, bool traceFailedRequests, string logPath, ISettingsResolver settingsResolver)
        {
            _logPath = logPath;
            _pathResolver = pathResolver;
            _traceFailedRequests = traceFailedRequests;
            _settingsResolver = settingsResolver;
        }

        public IEnumerable<string> GetSites()
        {
            var iis = new IIS.ServerManager();
            // The app pool is the app name
            return iis.Sites.Where(x => x.Name.StartsWith("kudu_"))
                            .Select(x => x.Applications[0].ApplicationPoolName)
                            .Distinct();
        }

        public Site GetSite(string applicationName)
        {
            var iis = new IIS.ServerManager();
            var mainSiteName = GetLiveSite(applicationName);
            var serviceSiteName = GetServiceSite(applicationName);
            var devSiteName = GetDevSite(applicationName);

            IIS.Site mainSite = iis.Sites[mainSiteName];

            if (mainSite == null)
            {
                return null;
            }

            IIS.Site serviceSite = iis.Sites[serviceSiteName];
            IIS.Site devSite = iis.Sites[devSiteName];

            var site = new Site();
            site.ServiceUrl = GetSiteUrl(serviceSite);
            site.DevSiteUrl = GetSiteUrl(devSite);
            site.SiteUrls = GetSiteUrls(mainSite);
            return site;
        }

        private string GetSiteUrl(IIS.Site site)
        {
            var urls = GetSiteUrls(site);

            if (urls == null)
            {
                return null;
            }
            else
            {
                return urls.FirstOrDefault();
            }
        }

        private List<string> GetSiteUrls(IIS.Site site)
        {
            var urls = new List<string>();

            if (site == null)
            {
                return null;
            }

            foreach (IIS.Binding binding in site.Bindings)
            {
                var builder = new UriBuilder
                {
                    Host = String.IsNullOrEmpty(binding.Host) ? "localhost" : binding.Host,
                    Scheme = binding.Protocol,
                    Port = binding.EndPoint.Port == 80 ? -1 : binding.EndPoint.Port
                };

                urls.Add(builder.ToString());
            }

            return urls;
        }

        public Site CreateSite(string applicationName)
        {
            var iis = new IIS.ServerManager();

            try
            {
                // Determine the host header values
                List<string> siteBindings = GetDefaultBindings(applicationName);

                // Create the service site for this site
                string serviceSiteName = GetServiceSite(applicationName);
                var serviceSite = CreateSite(iis, applicationName, serviceSiteName, _pathResolver.ServiceSitePath, siteBindings, true);

                IIS.Binding serviceSiteBinding = EnsureBinding(serviceSite.Bindings);
                int serviceSitePort = serviceSiteBinding.EndPoint.Port;

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

                var site = CreateSite(iis, applicationName, siteName, webRoot, siteBindings);

                IIS.Binding iisBinding = EnsureBinding(site.Bindings);
                int sitePort = iisBinding.EndPoint.Port;

                // Map a path called app to the site root under the service site
                MapServiceSitePath(iis, applicationName, Constants.MappedLiveSite, siteRoot);

                // Commit the changes to iis
                iis.CommitChanges();

                // Give IIS some time to create the site and map the path
                // REVIEW: Should we poll the site's state?
                Thread.Sleep(1000);

                var siteUrls = new List<string>();
                foreach (var url in site.Bindings)
                {
                    siteUrls.Add(String.Format("http://{0}:{1}/", String.IsNullOrEmpty(url.Host) ? "localhost" : url.Host, url.EndPoint.Port));
                }

                return new Site
                {
                    ServiceUrl = String.Format("http://localhost:{0}/", serviceSitePort),
                    SiteUrls = siteUrls
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

            // Determine the host header values
            List<string> siteBindings = GetDefaultBindings(applicationName);

            string devSiteName = GetDevSite(applicationName);

            IIS.Site site = iis.Sites[devSiteName];
            if (site == null)
            {
                // Get the path to the dev site
                string siteRoot = _pathResolver.GetDeveloperApplicationPath(applicationName);
                string webRoot = Path.Combine(siteRoot, Constants.WebRoot);
                var devSite = CreateSite(iis, applicationName, devSiteName, webRoot, siteBindings, true);

                // Ensure the directory is created
                FileSystemHelpers.EnsureDirectory(webRoot);

                // Map a path called app to the site root under the service site
                MapServiceSitePath(iis, applicationName, Constants.MappedDevSite, siteRoot);

                iis.CommitChanges();

                // Now we have a valid site
                site = devSite;
            }

            IIS.Binding iisBinding = EnsureBinding(site.Bindings);
            int sitePort = iisBinding.EndPoint.Port;
            var siteBinding = iisBinding.Host;

            siteUrl = String.Format("http://{0}:{1}/", siteBinding, sitePort);
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
                kuduAppPool.ProcessModel.LoadUserProfile = true;
                kuduAppPool.WaitForState(IIS.ObjectState.Started);

                SetupAcls(appName, appPoolName);
            }

            return kuduAppPool;
        }

        private IIS.Binding EnsureBinding(IIS.BindingCollection siteBindings)
        {
            if (siteBindings == null)
            {
                throw new NoHostNameFoundException();
            }

            IIS.Binding iisBinding = siteBindings.FirstOrDefault();

            if (iisBinding == null)
            {
                throw new NoHostNameFoundException();
            }

            return iisBinding;
        }

        private List<String> GetDefaultBindings(string applicationName)
        {
            var siteBindings = new List<string>();
            if (!String.IsNullOrWhiteSpace(_settingsResolver.SitesBaseUrl))
            {
                siteBindings.Add(applicationName + "." + _settingsResolver.SitesBaseUrl);
            }
            return siteBindings;
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

        private bool IsUrlAvailable(string url, IIS.ServerManager iis)
        {
            foreach (var iisSite in iis.Sites)
            {
                foreach (var binding in iisSite.Bindings)
                {
                    if (binding.Host != null && binding.Host.Equals(url, StringComparison.CurrentCultureIgnoreCase))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private IIS.Site CreateSite(IIS.ServerManager iis, string applicationName, string siteName, string siteRoot)
        {
            return CreateSite(iis, applicationName, siteName, siteRoot, null, true);
        }

        private IIS.Site CreateSite(IIS.ServerManager iis, string applicationName, string siteName, string siteRoot, List<string> siteBindings)
        {
            var randomPort = false;

            if (siteBindings == null || siteBindings.Count < 1)
            {
                randomPort = true;
            }

            return CreateSite(iis, applicationName, siteName, siteRoot, siteBindings, randomPort);
        }

        private IIS.Site CreateSite(IIS.ServerManager iis, string applicationName, string siteName, string siteRoot, List<string> siteBindings, bool randomPort)
        {
            var pool = EnsureAppPool(iis, applicationName);

            IIS.Site site;

            int sitePort = randomPort ? GetRandomPort(iis) : 80;

            if (siteBindings != null && siteBindings.Count > 0)
            {
                site = iis.Sites.Add(siteName, "http", String.Format("*:{0}:{1}", sitePort.ToString(), siteBindings.First()), siteRoot);
            }
            else
            {
                site = iis.Sites.Add(siteName, siteRoot, sitePort);
            }

            site.ApplicationDefaults.ApplicationPoolName = pool.Name;

            if (_traceFailedRequests)
            {
                site.TraceFailedRequestsLogging.Enabled = true;
                string path = Path.Combine(_logPath, applicationName, "Logs");
                Directory.CreateDirectory(path);
                site.TraceFailedRequestsLogging.Directory = path;
            }

            return site;
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

    public class NoHostNameFoundException : InvalidOperationException
    {
    }
}
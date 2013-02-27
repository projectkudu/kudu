using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using Kudu.Core.Infrastructure;
using IIS = Microsoft.Web.Administration;
using Microsoft.Web.Administration;

namespace Kudu.SiteManagement
{
    public class SiteManager : ISiteManager
    {
        private const string HostingStartHtml = "hostingstart.html";

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
            using (var iis = new IIS.ServerManager())
            {
                // The app pool is the app name
                return iis.Sites.Where(x => x.Name.StartsWith("kudu_", StringComparison.OrdinalIgnoreCase))
                                .Select(x => x.Applications[0].ApplicationPoolName)
                                .Distinct()
                                .ToList();
            }
        }

        public Site GetSite(string applicationName)
        {
            using (var iis = new IIS.ServerManager())
            {
                var mainSiteName = GetLiveSite(applicationName);
                var serviceSiteName = GetServiceSite(applicationName);

                IIS.Site mainSite = iis.Sites[mainSiteName];

                if (mainSite == null)
                {
                    return null;
                }

                IIS.Site serviceSite = iis.Sites[serviceSiteName];
                // IIS.Site devSite = iis.Sites[devSiteName];

                var site = new Site();
                site.ServiceUrl = GetSiteUrl(serviceSite);
                site.SiteUrls = GetSiteUrls(mainSite);
                return site;
            }
        }

        private static string GetSiteUrl(IIS.Site site)
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

        private static List<string> GetSiteUrls(IIS.Site site)
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
            using (var iis = new IIS.ServerManager())
            {
                try
                {
                    // Determine the host header values
                    List<string> siteBindings = GetDefaultBindings(applicationName, _settingsResolver.SitesBaseUrl);
                    List<string> serviceSiteBindings = GetDefaultBindings(applicationName, _settingsResolver.ServiceSitesBaseUrl);

                    // Create the service site for this site
                    string serviceSiteName = GetServiceSite(applicationName);
                    var serviceSite = CreateSite(iis, applicationName, serviceSiteName, _pathResolver.ServiceSitePath, serviceSiteBindings);

                    IIS.Binding serviceSiteBinding = EnsureBinding(serviceSite.Bindings);
                    int serviceSitePort = serviceSiteBinding.EndPoint.Port;

                    // Create the main site
                    string siteName = GetLiveSite(applicationName);
                    string siteRoot = _pathResolver.GetLiveSitePath(applicationName);
                    string webRoot = Path.Combine(siteRoot, Constants.WebRoot);

                    FileSystemHelpers.EnsureDirectory(webRoot);
                    File.WriteAllText(Path.Combine(webRoot, HostingStartHtml), @"<html> 
<head>
<title>This web site has been successfully created</title>
<style type=""text/css"">
 BODY { color: #444444; background-color: #E5F2FF; font-family: verdana; margin: 0px; text-align: center; margin-top: 100px; }
 H1 { font-size: 16pt; margin-bottom: 4px; }
</style>
</head>
<body>
<h1>This web site has been successfully created</h1><br/>
</body> 
</html>");

                    var site = CreateSite(iis, applicationName, siteName, webRoot, siteBindings);

                    // Map a path called app to the site root under the service site
                    MapServiceSitePath(iis, applicationName, Constants.MappedSite, siteRoot);

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
        }

        public void DeleteSite(string applicationName)
        {
            using (var iis = new IIS.ServerManager())
            {
                // Get the app pool for this application
                string appPoolName = GetAppPool(applicationName);
                IIS.ApplicationPool kuduPool = iis.ApplicationPools[appPoolName];

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

                try
                {
                    DeleteSafe(sitePath);
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

                    // Clear out the app pool user profile directory if it exists
                    string userDir = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar));
                    string appPoolDirectory = Path.Combine(userDir, appPoolName);
                    DeleteSafe(appPoolDirectory);
                }
            }
        }

        public void SetSiteWebRoot(string applicationName, string siteRoot)
        {
            using (var iis = new IIS.ServerManager())
            {
                string siteName = GetDevSite(applicationName);

                IIS.Site site = iis.Sites[siteName];
                if (site != null)
                {
                    string sitePath = _pathResolver.GetLiveSitePath(applicationName);
                    string webRoot = Path.Combine(sitePath, Constants.WebRoot, siteRoot);

                    // Change the web root
                    site.Applications[0].VirtualDirectories[0].PhysicalPath = webRoot;

                    iis.CommitChanges();

                    Thread.Sleep(1000);
                }
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

        private static IIS.ApplicationPool EnsureAppPool(IIS.ServerManager iis, string appName)
        {
            string appPoolName = GetAppPool(appName);
            var kuduAppPool = iis.ApplicationPools[appPoolName];
            if (kuduAppPool == null)
            {
                iis.ApplicationPools.Add(appPoolName);
                iis.CommitChanges();
                kuduAppPool = iis.ApplicationPools[appPoolName];
                kuduAppPool.ManagedPipelineMode = IIS.ManagedPipelineMode.Integrated;
                kuduAppPool.ManagedRuntimeVersion = "v4.0";
                kuduAppPool.AutoStart = true;
                kuduAppPool.ProcessModel.LoadUserProfile = true;
                kuduAppPool.WaitForState(IIS.ObjectState.Started);
            }

            return kuduAppPool;
        }

        private static IIS.Binding EnsureBinding(IIS.BindingCollection siteBindings)
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

        private static List<String> GetDefaultBindings(string applicationName, string baseUrl)
        {
            var siteBindings = new List<string>();
            if (!String.IsNullOrWhiteSpace(baseUrl))
            {
                string binding = CreateBindingInformation(applicationName, baseUrl);
                siteBindings.Add(binding);
            }
            return siteBindings;
        }

        //TODO this is duplicated in HgServer.cs, though out of sync in functionality.
        private static int GetRandomPort(IIS.ServerManager iis)
        {
            int randomPort = portNumberGenRnd.Next(1025, 65535);
            while (!IsAvailable(randomPort, iis))
            {
                randomPort = portNumberGenRnd.Next(1025, 65535);
            }

            return randomPort;
        }

        //TODO this is duplicated in HgServer.cs, though out of sync in functionality.
        private static bool IsAvailable(int port, IIS.ServerManager iis)
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

        private IIS.Site CreateSite(IIS.ServerManager iis, string applicationName, string siteName, string siteRoot, List<string> siteBindings)
        {
            var pool = EnsureAppPool(iis, applicationName);

            EnsureDefaultDocument(iis);

            IIS.Site site;

            if (siteBindings != null && siteBindings.Count > 0)
            {
                site = iis.Sites.Add(siteName, "http", siteBindings.First(), siteRoot);
            }
            else
            {
                int sitePort = GetRandomPort(iis);
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

        private static void EnsureDefaultDocument(IIS.ServerManager iis)
        {
            Configuration applicationHostConfiguration = iis.GetApplicationHostConfiguration();
            ConfigurationSection defaultDocumentSection = applicationHostConfiguration.GetSection("system.webServer/defaultDocument");

            ConfigurationElementCollection filesCollection = defaultDocumentSection.GetCollection("files");

            if (!filesCollection.Any(ConfigurationElementContainsHostingStart))
            {
                ConfigurationElement addElement = filesCollection.CreateElement("add");

                addElement["value"] = HostingStartHtml;
                filesCollection.Add(addElement);

                iis.CommitChanges();
            }
        }

        private static bool ConfigurationElementContainsHostingStart(ConfigurationElement configurationElement)
        {
            object valueAttribute = configurationElement["value"];

            return valueAttribute != null && String.Equals(HostingStartHtml, valueAttribute.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateBindingInformation(string applicationName, string baseUrl, string defaultIp = "*", string defaultPort = "80")
        {
            // Creates the 'bindingInformation' parameter for IIS.ServerManager.Sites.Add()
            // Accepts baseUrl in 3 formats: hostname, hostname:port and ip:port:hostname
            
            // Based on the default parameters, applicationName + baseUrl it creates
            // a string in the format ip:port:hostname

            string[] parts = baseUrl.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
            string ip = defaultIp;
            string host = string.Empty;
            string port = defaultPort;

            switch (parts.Length)
            {
                case 1: // kudu.mydomain
                    host = parts[0];
                    break;
                case 2: // kudu.mydomain:8080
                    host = parts[0];
                    port = parts[1];
                    break;
                case 3: // 192.168.100.3:80:kudu.mydomain
                    ip = parts[0];
                    port = parts[1];
                    host = parts[2];
                    break;
            }

            return String.Format("{0}:{1}:{2}", ip, port, applicationName + "." + host);
        }

        private static void DeleteSite(IIS.ServerManager iis, string siteName, bool deletePhysicalFiles = true)
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
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Infrastructure;
using Kudu.SiteManagement.Configuration;
using Microsoft.Web.Administration;
using IIS = Microsoft.Web.Administration;

namespace Kudu.SiteManagement
{
    public class SiteManager : ISiteManager
    {
        private const string HostingStartHtml = "hostingstart.html";
        private const string HostingStartHtmlContents = @"<html>
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
</html>";

        private readonly static Random portNumberGenRnd = new Random((int)DateTime.UtcNow.Ticks);

        private readonly string _logPath;
        private readonly bool _traceFailedRequests;
        private readonly IPathResolver _pathResolver;
        private readonly IKuduConfiguration _configuration;

        public SiteManager(IPathResolver pathResolver, IKuduConfiguration configuration)
            : this(pathResolver, configuration, false, null)
        {
        }

        public SiteManager(IPathResolver pathResolver, IKuduConfiguration configuration, bool traceFailedRequests, string logPath)
        {
            _logPath = logPath;
            _pathResolver = pathResolver;
            _traceFailedRequests = traceFailedRequests;
            _configuration = configuration;
        }

        public IEnumerable<string> GetSites()
        {
            using (var iis = GetServerManager())
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
            using (var iis = GetServerManager())
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
                site.ServiceUrls = GetSiteUrls(serviceSite);
                site.SiteUrls = GetSiteUrls(mainSite);
                return site;
            }
        }

        private static List<string> GetSiteUrls(IIS.Site site)
        {
            var urls = new List<string>();

            if (site == null)
            {
                return null;
            }

            foreach (Binding binding in site.Bindings)
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

        public async Task<Site> CreateSiteAsync(string applicationName)
        {
            using (ServerManager iis = GetServerManager())
            {
                try
                {
                    // Determine the host header values
                    List<string> siteBindings = BuildDefaultBindings(applicationName, _configuration.ApplicationBase).ToList();
                    List<string> serviceSiteBindings = BuildDefaultBindings(applicationName, _configuration.ServiceBase).ToList();

                    // Create the service site for this site
                    var serviceSite = CreateSiteAsync(iis, applicationName, GetServiceSite(applicationName), _configuration.ServiceSitePath, serviceSiteBindings);

                    // Create the main site
                    string siteName = GetLiveSite(applicationName);
                    string root = _pathResolver.GetApplicationPath(applicationName);
                    string siteRoot = _pathResolver.GetLiveSitePath(applicationName);
                    string webRoot = Path.Combine(siteRoot, Constants.WebRoot);

                    FileSystemHelpers.EnsureDirectory(webRoot);
                    File.WriteAllText(Path.Combine(webRoot, HostingStartHtml), HostingStartHtmlContents);

                    var site = CreateSiteAsync(iis, applicationName, siteName, webRoot, siteBindings);

                    // Map a path called _app to the site root under the service site
                    MapServiceSitePath(iis, applicationName, Constants.MappedSite, root);

                    // Commit the changes to iis
                    iis.CommitChanges();

                    var serviceUrls = new List<string>();
                    foreach (var url in serviceSite.Bindings)
                    {
                        serviceUrls.Add(String.Format("http://{0}:{1}/", String.IsNullOrEmpty(url.Host) ? "localhost" : url.Host, url.EndPoint.Port));
                    }

                    // Wait for the site to start
                    await OperationManager.AttemptAsync(() => WaitForSiteAsync(serviceUrls[0]));

                    // Set initial ScmType state to LocalGit
                    var settings = new RemoteDeploymentSettingsManager(serviceUrls.First() + "api/settings");
                    await settings.SetValue(SettingsKeys.ScmType, ScmType.LocalGit);

                    var siteUrls = new List<string>();
                    foreach (var url in site.Bindings)
                    {
                        siteUrls.Add(String.Format("http://{0}:{1}/", String.IsNullOrEmpty(url.Host) ? "localhost" : url.Host, url.EndPoint.Port));
                    }

                    return new Site
                    {
                        ServiceUrls = serviceUrls,
                        SiteUrls = siteUrls
                    };
                }
                catch
                {
                    try
                    {
                        DeleteSiteAsync(applicationName).Wait();
                    }
                    catch
                    {
                        // Don't let it throw if we're unable to delete a failed creation.
                    }
                    throw;
                }
            }
        }

        private IEnumerable<string> BuildDefaultBindings(string applicationName, IUrlConfiguration baseUrl)
        {
            if(baseUrl != null) yield return CreateBindingInformation(applicationName, baseUrl.Url);
        }

        public async Task DeleteSiteAsync(string applicationName)
        {
            using (var iis = GetServerManager())
            {
                // Get the app pool for this application
                string appPoolName = GetAppPool(applicationName);
                ApplicationPool kuduPool = iis.ApplicationPools[appPoolName];

                if (kuduPool == null)
                {
                    // If there's no app pool then do nothing
                    return;
                }

                await Task.WhenAll(
                    DeleteSiteAsync(iis, GetLiveSite(applicationName)),
                    // Don't delete the physical files for the service site
                    DeleteSiteAsync(iis, GetServiceSite(applicationName), deletePhysicalFiles: false)
                );

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

        private static string NormalizeBinding(string binding)
        {
            //Note: Seems like http and https is the two IIS allows when adding bindings to a site, nothing else.
            if (binding.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return binding;

            if (binding.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                return binding;

            return "http://" + binding;
        }

        public bool AddSiteBinding(string applicationName, string siteBinding, SiteType siteType)
        {
            var uri = new Uri(NormalizeBinding(siteBinding));
            try
            {
                using (ServerManager iis = GetServerManager())
                {
                    if (!IsAvailable(uri.Host, uri.Port, iis))
                    {
                        return false;
                    }

                    IIS.Site site = siteType == SiteType.Live
                        ? iis.Sites[GetLiveSite(applicationName)] 
                        : iis.Sites[GetServiceSite(applicationName)];

                    if (site == null)
                        return true;

                    site.Bindings.Add("*:" + uri.Port + ":" + uri.Host, uri.Scheme);
                    iis.CommitChanges();

                    Thread.Sleep(1000);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool RemoveSiteBinding(string applicationName, string siteBinding, SiteType siteType)
        {
            try
            {
                using (var iis = GetServerManager())
                {
                    IIS.Site site = siteType == SiteType.Live 
                        ? iis.Sites[GetLiveSite(applicationName)] 
                        : iis.Sites[GetServiceSite(applicationName)];

                    if (site == null) 
                        return true;
                    
                    var uri = new Uri(siteBinding);
                    var binding = site.Bindings
                        .FirstOrDefault(x => x.Host.Equals(uri.Host)
                            && x.EndPoint.Port.Equals(uri.Port)
                            && x.Protocol.Equals(uri.Scheme));

                    if (binding == null) 
                        return true;
                        
                    site.Bindings.Remove(binding);
                    iis.CommitChanges();
                    Thread.Sleep(1000);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void MapServiceSitePath(ServerManager iis, string applicationName, string path, string siteRoot)
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

        private static ApplicationPool EnsureAppPool(ServerManager iis, string appName)
        {
            string appPoolName = GetAppPool(appName);
            var kuduAppPool = iis.ApplicationPools[appPoolName];
            if (kuduAppPool == null)
            {
                iis.ApplicationPools.Add(appPoolName);
                iis.CommitChanges();
                kuduAppPool = iis.ApplicationPools[appPoolName];
                kuduAppPool.ManagedPipelineMode = ManagedPipelineMode.Integrated;
                kuduAppPool.ManagedRuntimeVersion = "v4.0";
                kuduAppPool.AutoStart = true;
                kuduAppPool.ProcessModel.LoadUserProfile = true;
            }

            EnsureDefaultDocument(iis);

            return kuduAppPool;
        }


        private static int GetRandomPort(ServerManager iis)
        {
            int randomPort = portNumberGenRnd.Next(1025, 65535);
            while (!IsAvailable(randomPort, iis))
            {
                randomPort = portNumberGenRnd.Next(1025, 65535);
            }

            return randomPort;
        }

        private static bool IsAvailable(int port, ServerManager iis)
        {
            var tcpConnections = IPGlobalProperties.GetIPGlobalProperties().GetActiveTcpConnections();
            return tcpConnections.All(connectionInfo => connectionInfo.LocalEndPoint.Port != port)
                && iis.Sites
                    .SelectMany(iisSite => iisSite.Bindings)
                    .All(binding => binding.EndPoint == null || binding.EndPoint.Port != port);
        }

        private static bool IsAvailable(string host, int port, ServerManager iis)
        {
            return iis.Sites
                .SelectMany(iisSite => iisSite.Bindings)
                .All(binding => binding.EndPoint == null || binding.EndPoint.Port != port || binding.Host != host);
        }

        private IIS.Site CreateSiteAsync(ServerManager iis, string applicationName, string siteName, string siteRoot, List<string> siteBindings)
        {
            var pool = EnsureAppPool(iis, applicationName);

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

        private static void EnsureDefaultDocument(ServerManager iis)
        {
            IIS.Configuration applicationHostConfiguration = iis.GetApplicationHostConfiguration();
            ConfigurationSection defaultDocumentSection = applicationHostConfiguration.GetSection("system.webServer/defaultDocument");

            ConfigurationElementCollection filesCollection = defaultDocumentSection.GetCollection("files");

            if (filesCollection.Any(ConfigurationElementContainsHostingStart))
                return;

            ConfigurationElement addElement = filesCollection.CreateElement("add");
            addElement["value"] = HostingStartHtml;
            filesCollection.Add(addElement);

            iis.CommitChanges();
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

        private static Task DeleteSiteAsync(ServerManager iis, string siteName, bool deletePhysicalFiles = true)
        {
            var site = iis.Sites[siteName];
            if (site != null)
            {
                return OperationManager.AttemptAsync(async () =>
                {
                    await Task.Run(() =>
                    {
                        if (deletePhysicalFiles)
                        {
                            string physicalPath = site.Applications[0].VirtualDirectories[0].PhysicalPath;
                            DeleteSafe(physicalPath);
                        }
                        iis.Sites.Remove(site);
                    });
                });
            }

            return Task.FromResult(0);
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

        private static ServerManager GetServerManager()
        {
            return new ServerManager(Environment.ExpandEnvironmentVariables("%windir%\\system32\\inetsrv\\config\\applicationHost.config"));
        }

        private static async Task WaitForSiteAsync(string serviceUrl)
        {
            using (var client = new HttpClient())
            {
                using (var response = await client.GetAsync(serviceUrl))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }
    }
}
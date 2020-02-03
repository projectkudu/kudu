using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Core.Infrastructure;
using Kudu.SiteManagement.Certificates;
using Kudu.SiteManagement.Configuration;
using Kudu.SiteManagement.Configuration.Section;
using Kudu.SiteManagement.Context;
using Microsoft.Web.Administration;
using IIS = Microsoft.Web.Administration;

namespace Kudu.SiteManagement
{
    public class SiteManager : ISiteManager
    {
        private const string HostingStartHtml = "hostingstart.html";
        private const string HostingStartHtmlContents = @"<html>
<head>
<title>This web site is up and running</title>
<style type=""text/css"">
    BODY { color: #444444; background-color: #E5F2FF; font-family: verdana; margin: 0px; text-align: center; margin-top: 100px; }
    H1 { font-size: 16pt; margin-bottom: 4px; }
</style>
</head>
<body>
<h1>This web site is up and running</h1><br/>
</body> 
</html>";

        private readonly static Random portNumberGenRnd = new Random((int)DateTime.UtcNow.Ticks);

        private readonly string _logPath;
        private readonly bool _traceFailedRequests;
        private readonly IKuduContext _context;
        private readonly ICertificateSearcher _certificateSearcher;

        public SiteManager(IKuduContext context, ICertificateSearcher certificateSearcher)
            : this(context, certificateSearcher, false, null)
        {
        }

        public SiteManager(IKuduContext context, ICertificateSearcher certificateSearcher, bool traceFailedRequests, string logPath)
        {
            _logPath = logPath;
            _traceFailedRequests = traceFailedRequests;
            _context = context;
            _certificateSearcher = certificateSearcher;
        }

        public IEnumerable<string> GetSites()
        {
            using (var iis = GetServerManager())
            {
                try
                {
                    // The app pool is the app name
                    return iis.Sites.Where(x => x.Name.StartsWith("kudu_", StringComparison.OrdinalIgnoreCase))
                                    .Select(x => x.Applications[0].ApplicationPoolName)
                                    .Distinct()
                                    .ToList();
                }
                catch
                {
                    return new List<string>();
                }
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
            if (site == null)
            { return null; }

            return site.Bindings.Select(binding => new UriBuilder
            {
                Host = string.IsNullOrEmpty(binding.Host) ? "localhost" : binding.Host,
                Scheme = binding.Protocol,
                Port = binding.EndPoint.Port == 80 ? -1 : binding.EndPoint.Port
            }).Select(builder => builder.ToString()).ToList();
        }

        public async Task<Site> CreateSiteAsync(string applicationName)
        {
            using (ServerManager iis = GetServerManager())
            {
                try
                {
                    var siteBindingCongfigs = new List<IBindingConfiguration>();
                    var svcSiteBindingCongfigs = new List<IBindingConfiguration>();
                    if (_context.Configuration != null && _context.Configuration.Bindings != null)
                    {
                        siteBindingCongfigs = _context.Configuration.Bindings.Where(b => b.SiteType == SiteType.Live).ToList();
                        svcSiteBindingCongfigs = _context.Configuration.Bindings.Where(b => b.SiteType == SiteType.Service).ToList();
                    }

                    // Determine the host header values
                    List<BindingInformation> siteBindings = BuildDefaultBindings(applicationName, siteBindingCongfigs).ToList();
                    List<BindingInformation> serviceSiteBindings = BuildDefaultBindings(applicationName, svcSiteBindingCongfigs).ToList();

                    // Create the service site for this site
                    var serviceSite = CreateSiteAsync(iis, applicationName, GetServiceSite(applicationName), _context.Configuration.ServiceSitePath, serviceSiteBindings);

                    // Create the main site
                    string siteName = GetLiveSite(applicationName);
                    string root = _context.Paths.GetApplicationPath(applicationName);
                    string siteRoot = _context.Paths.GetLiveSitePath(applicationName);
                    string webRoot = Path.Combine(siteRoot, Constants.WebRoot);

                    FileSystemHelpers.EnsureDirectory(webRoot);
                    File.WriteAllText(Path.Combine(webRoot, HostingStartHtml), HostingStartHtmlContents);

                    var site = CreateSiteAsync(iis, applicationName, siteName, webRoot, siteBindings);

                    // Map a path called _app to the site root under the service site
                    MapServiceSitePath(iis, applicationName, Constants.MappedSite, root);

                    // Commit the changes to iis
                    iis.CommitChanges();

                    var serviceUrls = serviceSite.Bindings
                        .Select(url => String.Format("{0}://{1}:{2}/", url.Protocol, String.IsNullOrEmpty(url.Host) ? "localhost" : url.Host, url.EndPoint.Port))
                        .ToList();

                    // Wait for the site to start
                    await OperationManager.AttemptAsync(() => WaitForSiteAsync(serviceUrls.First()));

                    // Set initial ScmType state to LocalGit
                    var credentials = _context.Configuration.BasicAuthCredential.GetCredentials();
                    var settings = new RemoteDeploymentSettingsManager(serviceUrls.First() + "api/settings", credentials);
                    await settings.SetValue(SettingsKeys.ScmType, ScmType.LocalGit);

                    var siteUrls = site.Bindings
                        .Select(url => String.Format("{0}://{1}:{2}/", url.Protocol, String.IsNullOrEmpty(url.Host) ? "localhost" : url.Host, url.EndPoint.Port))
                        .ToList();

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
                        await DeleteSiteAsync(applicationName);
                    }
                    catch
                    {
                        // Don't let it throw if we're unable to delete a failed creation.
                    }
                    throw;
                }
            }
        }

        //NOTE: Small temporary object for configuration.
        private struct BindingInformation
        {
            public string Binding { get; set; }
            public IBindingConfiguration Configuration { get; set; }
            //public string Url { get { return Configuration.Url; } }
            public UriScheme Scheme { get { return Configuration.Scheme; } }
            //public SiteType SiteType { get { return Configuration.SiteType; } }
            public string Certificate { get { return Configuration.Certificate; } }
        }

        private static IEnumerable<BindingInformation> BuildDefaultBindings(string applicationName, IEnumerable<IBindingConfiguration> bindings)
        {
            return bindings.Select(configuration => configuration.Scheme == UriScheme.Http
                ? new BindingInformation { Configuration = configuration, Binding = CreateBindingInformation(applicationName, configuration.Url) }
                : new BindingInformation { Configuration = configuration, Binding = CreateBindingInformation(applicationName, configuration.Url, defaultPort: "443") })
                //NOTE: We order the bindings so we get the http bindings on top, this means we can easily prioritise those for testing site setup later.
                .OrderBy(b => b.Scheme);
        }

        public async Task DeleteSiteAsync(string applicationName)
        {
            string appPoolName = GetAppPool(applicationName);
            using (var iis = GetServerManager())
            {
                // Get the app pool for this application
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


                try
                {
                    iis.CommitChanges();
                    Thread.Sleep(1000);
                }
                catch (NotImplementedException)
                {
                    //NOTE: For some reason, deleting a site with a HTTPS bindings results in a NotImplementedException on Windows 7, but it seems to remove everything relevant anyways.
                }
            }

            //NOTE: DeleteSiteAsync was not split into to usings before, but by calling CommitChanges midway, the iis manager goes into a read-only mode on Windows7 which then provokes
            //      an error on the next commit. On the next pass. Acquirering a new Manager seems like a more safe approach.
            using (var iis = GetServerManager())
            {
                string appPath = _context.Paths.GetApplicationPath(applicationName);
                string sitePath = _context.Paths.GetLiveSitePath(applicationName);
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

                    iis.ApplicationPools.Remove(iis.ApplicationPools[appPoolName]);
                    iis.CommitChanges();

                    // Clear out the app pool user profile directory if it exists
                    string userDir = Path.GetDirectoryName(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).TrimEnd(Path.DirectorySeparatorChar));
                    string appPoolDirectory = Path.Combine(userDir, appPoolName);
                    DeleteSafe(appPoolDirectory);
                }
            }
        }

        public bool AddSiteBinding(string applicationName, KuduBinding binding)
        {
            try
            {
                using (ServerManager iis = GetServerManager())
                {
                    if (!IsAvailable(binding.Host, binding.Port, iis))
                    {
                        return false;
                    }

                    IIS.Site site = binding.SiteType == SiteType.Live
                        ? iis.Sites[GetLiveSite(applicationName)]
                        : iis.Sites[GetServiceSite(applicationName)];

                    if (site == null)
                    {
                        return true;
                    }

                    string bindingInformation = string.Format("{0}:{1}:{2}", binding.Ip, binding.Port, binding.Host);
                    switch (binding.Schema)
                    {
                        case UriScheme.Http:
                            site.Bindings.Add(bindingInformation, "http");
                            break;

                        case UriScheme.Https:
                            Certificate cert = _certificateSearcher.Lookup(binding.Certificate).ByThumbprint();
                            Binding bind = site.Bindings.Add(bindingInformation, cert.GetCertHash(), cert.StoreName);
                            if (binding.Sni)
                            {
                                bind.SetAttributeValue("sslFlags", SslFlags.Sni);
                            }

                            break;
                    }
                    iis.CommitChanges();
                    Thread.Sleep(1000);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
                return false;
            }
        }

        public bool RemoveSiteBinding(string applicationName, string siteBinding, SiteType siteType)
        {
            try
            {
                using (ServerManager iis = GetServerManager())
                {
                    IIS.Site site = siteType == SiteType.Live
                        ? iis.Sites[GetLiveSite(applicationName)]
                        : iis.Sites[GetServiceSite(applicationName)];

                    if (site == null)
                    { return true; }

                    Uri uri = new Uri(siteBinding);
                    Binding binding = site.Bindings
                        .FirstOrDefault(x => x.Host.Equals(uri.Host)
                            && x.EndPoint.Port.Equals(uri.Port)
                            && x.Protocol.Equals(uri.Scheme));

                    if (binding == null)
                    { return true; }

                    site.Bindings.Remove(binding);
                    iis.CommitChanges();
                    Thread.Sleep(1000);
                }
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex);
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
                kuduAppPool.Failure.RapidFailProtection = false;

                // We've seen strange errors after switching to VS 2015 msbuild when using App Pool Identity.
                // The errors look like:
                // error CS0041 : Unexpected error writing debug information -- 'Retrieving the COM class factory for component with CLSID {0AE2DEB0-F901-478B-BB9F-881EE8066788} failed due to the following error : 800703fa Illegal operation attempted on a registry key that has been marked for deletion. (Exception from HRESULT: 0x800703FA).'
                // To work around this, we're using NetworkService. But it would be good to understand the root
                // cause of the issue.
                if (ConfigurationManager.AppSettings["UseNetworkServiceIdentity"] == "true")
                {
                    kuduAppPool.ProcessModel.IdentityType = ProcessModelIdentityType.NetworkService;
                }
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

        private IIS.Site CreateSiteAsync(ServerManager iis, string applicationName, string siteName, string siteRoot, List<SiteManager.BindingInformation> bindings)
        {
            var pool = EnsureAppPool(iis, applicationName);

            IIS.Site site;
            if (bindings.Any())
            {
                BindingInformation first = bindings.First();

                //Binding primaryBinding;
                site = first.Scheme == UriScheme.Http
                    ? iis.Sites.Add(siteName, "http", first.Binding, siteRoot)
                    : iis.Sites.Add(siteName, first.Binding, siteRoot, _certificateSearcher.Lookup(first.Certificate).ByFriendlyName().GetCertHash());
                if (first.Configuration.RequireSni)
                {
                    site.Bindings.First().SetAttributeValue("sslFlags", SslFlags.Sni);
                }

                //Note: Add the rest of the bindings normally.
                foreach (BindingInformation binding in bindings.Skip(1))
                {
                    switch (binding.Scheme)
                    {
                        case UriScheme.Http:
                            site.Bindings.Add(binding.Binding, "http");
                            break;

                        case UriScheme.Https:
                            Certificate cert = _certificateSearcher.Lookup(binding.Certificate).ByFriendlyName();
                            if (cert == null)
                            {
                                throw new ConfigurationErrorsException(string.Format("Could not find a certificate by the name '{0}'.", binding.Certificate));
                            }

                            Binding bind = site.Bindings.Add(binding.Binding, cert.GetCertHash(), cert.StoreName);
                            if (binding.Configuration.RequireSni)
                            {
                                bind.SetAttributeValue("sslFlags", SslFlags.Sni);
                            }
                            break;
                    }
                }
            }
            else
            {
                site = iis.Sites.Add(siteName, siteRoot, GetRandomPort(iis));
            }

            site.ApplicationDefaults.ApplicationPoolName = pool.Name;

            if (!_traceFailedRequests)
                return site;

            site.TraceFailedRequestsLogging.Enabled = true;
            string path = Path.Combine(_logPath, applicationName, "Logs");
            Directory.CreateDirectory(path);
            site.TraceFailedRequestsLogging.Directory = path;

            return site;
        }

        private static void EnsureDefaultDocument(ServerManager iis)
        {
            IIS.Configuration applicationHostConfiguration = iis.GetApplicationHostConfiguration();
            IIS.ConfigurationSection defaultDocumentSection = applicationHostConfiguration.GetSection("system.webServer/defaultDocument");

            IIS.ConfigurationElementCollection filesCollection = defaultDocumentSection.GetCollection("files");

            if (filesCollection.Any(ConfigurationElementContainsHostingStart))
                return;

            IIS.ConfigurationElement addElement = filesCollection.CreateElement("add");
            addElement["value"] = HostingStartHtml;
            filesCollection.Add(addElement);

            iis.CommitChanges();
        }

        private static bool ConfigurationElementContainsHostingStart(IIS.ConfigurationElement configurationElement)
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

        private ServerManager GetServerManager()
        {
            return new ServerManager(_context.Configuration.IISConfigurationFile);
        }

        private async Task WaitForSiteAsync(string serviceUrl)
        {
            var credentials = _context.Configuration.BasicAuthCredential.GetCredentials();
            using (var client = HttpClientHelper.CreateClient(serviceUrl, credentials))
            {
                using (var response = await client.GetAsync(serviceUrl))
                {
                    response.EnsureSuccessStatusCode();
                }
            }
        }
    }
}
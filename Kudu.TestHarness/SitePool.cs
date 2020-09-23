using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using Kudu.SiteManagement;
using Kudu.SiteManagement.Certificates;
using Kudu.SiteManagement.Configuration;
using Kudu.SiteManagement.Context;
using Kudu.TestHarness.Xunit;
using Kudu.Client.Infrastructure;

namespace Kudu.TestHarness
{
    public static class SitePool
    {
        private static readonly string _sitePrefix = KuduUtils.SiteReusedForAllTests;
        private static readonly ConcurrentStack<int> _availableSiteIndex = new ConcurrentStack<int>(Enumerable.Range(1, KuduXunitTestRunnerUtils.MaxParallelThreads).Reverse());
        private static readonly object _createSiteLock = new object();

        public static async Task<ApplicationManager> CreateApplicationAsync()
        {
            int siteIndex;

            // try till succeeded
            while (!_availableSiteIndex.TryPop(out siteIndex))
            {
                await Task.Delay(5000);
            }

            try
            {
                // if succeed, caller will push back the index.
                return await CreateApplicationInternal(siteIndex);
            }
            catch
            {
                _availableSiteIndex.Push(siteIndex);

                throw;
            }
        }

        public static void ReportTestCompletion(ApplicationManager applicationManager, bool success)
        {
            _availableSiteIndex.Push(applicationManager.SitePoolIndex);
        }

        private static async Task<ApplicationManager> CreateApplicationInternal(int siteIndex)
        {
            string applicationName = _sitePrefix + siteIndex;

            string operationName = "SitePool.CreateApplicationInternal " + applicationName;

            var context = new KuduTestContext();
            var siteManager = GetSiteManager(context);

            Site site = siteManager.GetSite(applicationName);
            if (site != null)
            {
                TestTracer.Trace("{0} Site already exists at {1}. Reusing site", operationName, site.SiteUrl);

                RunAgainstCustomKuduUrlIfRequired(site);

                var appManager = new ApplicationManager(siteManager, site, applicationName)
                {
                    SitePoolIndex = siteIndex
                };

                if (!KuduUtils.RunningAgainstLinuxKudu)
                {
                    // In site reuse mode, clean out the existing site so we start clean
                    // Enumrate all w3wp processes and make sure to kill any process with an open handle to klr.host.dll
                    foreach (var process in (await appManager.ProcessManager.GetProcessesAsync()).Where(p => p.Name.Equals("w3wp", StringComparison.OrdinalIgnoreCase)))
                    {
                        var extendedProcess = await appManager.ProcessManager.GetProcessAsync(process.Id);
                        if (extendedProcess.OpenFileHandles.Any(h => h.IndexOf("dnx.host.dll", StringComparison.OrdinalIgnoreCase) != -1))
                        {
                            await appManager.ProcessManager.KillProcessAsync(extendedProcess.Id, throwOnError: false);
                        }
                    }
                }

                await appManager.RepositoryManager.Delete(deleteWebRoot: true, ignoreErrors: true);

                // Nuke directories of interest to start the tests with a clean slate
                appManager.VfsManager.Delete("site/libs", recursive: true);
                appManager.VfsManager.Delete("site/scripts", recursive: true);

                // Make sure we start with the correct default file as some tests expect it
                WriteIndexHtml(appManager);

                TestTracer.Trace("{0} completed", operationName);
                return appManager;
            }
            else
            {
                TestTracer.Trace("{0} Creating new site", operationName);
                lock (_createSiteLock)
                {
                    if (ConfigurationManager.AppSettings["UseNetworkServiceIdentity"] == "true")
                    {
                        var applicationsPath = context.Configuration.ApplicationsPath;
                        if (!Directory.Exists(applicationsPath))
                        {
                            Directory.CreateDirectory(applicationsPath);

                            var accessRule = new FileSystemAccessRule("NETWORK SERVICE",
                                                 fileSystemRights: FileSystemRights.Modify | FileSystemRights.Write | FileSystemRights.ReadAndExecute | FileSystemRights.Read | FileSystemRights.ListDirectory,
                                                 inheritanceFlags: InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                                                 propagationFlags: PropagationFlags.None,
                                                 type: AccessControlType.Allow);

                            var directoryInfo = new DirectoryInfo(applicationsPath);
                            DirectorySecurity directorySecurity = directoryInfo.GetAccessControl();
                            directorySecurity.AddAccessRule(accessRule);
                            directoryInfo.SetAccessControl(directorySecurity);
                        }
                    }

                    site = siteManager.CreateSiteAsync(applicationName).Result;

                    RunAgainstCustomKuduUrlIfRequired(site);
                }

                TestTracer.Trace("{0} Created new site at {1}", operationName, site.SiteUrl);
                return new ApplicationManager(siteManager, site, applicationName)
                {
                    SitePoolIndex = siteIndex
                };
            }
        }

        private static void RunAgainstCustomKuduUrlIfRequired(Site site)
        {
            if (!string.IsNullOrWhiteSpace(KuduUtils.CustomKuduUrl))
            {
                site.ServiceUrls = new List<string> { KuduUtils.CustomKuduUrl };
            }
        }

        private static ISiteManager GetSiteManager(IKuduContext context)
        {
            //TODO: Mock Searcher.
            return new SiteManager(context, new CertificateSearcher(context.Configuration), true, PathHelper.TestResultsPath);
        }

        // Try to write index.html.  In case of failure with 502, we will include
        // UpTime information with the exception.  This info will be used for furthur
        // investigation of Bad Gateway issue.
        private static void WriteIndexHtml(ApplicationManager appManager)
        {
            try
            {
                appManager.VfsWebRootManager.WriteAllText("hostingstart.html", "<h1>This web site is up and running</h1>");
            }
            catch (HttpRequestException ex)
            {
                if (ex.Message.Contains("502 (Bad Gateway)"))
                {
                    string upTime = null;
                    try
                    {
                        upTime = appManager.GetKuduUpTime();
                    }
                    catch (Exception exception)
                    {
                        TestTracer.Trace("GetKuduUpTime failed with exception\n{0}", exception);
                    }

                    if (!String.IsNullOrEmpty(upTime))
                    {
                        throw new HttpRequestException(ex.Message + " Kudu Up Time: " + upTime, ex);
                    }
                }

                throw;
            }
        }
    }

    public class KuduTestContext : IKuduContext
    {
        public IPathResolver Paths { get; private set; }
        public IKuduConfiguration Configuration { get; private set; }
        public Version IISVersion { get; private set; }
        public IEnumerable<string> IPAddresses { get; private set; }

        public KuduTestContext()
        {
            Paths = new PathResolver(Configuration = new KuduTestConfiguration());
        }
    }

    public class KuduTestConfiguration : IKuduConfiguration
    {
        public Version IISVersion { get; private set; }
        public IEnumerable<string> IPAddresses { get; private set; }

        public string RootPath { get; private set; }
        public string ApplicationsPath { get; private set; }
        public string ServiceSitePath { get; private set; }
        public string IISConfigurationFile { get; private set; }
        public bool CustomHostNamesEnabled { get; private set; }
        public IEnumerable<IBindingConfiguration> Bindings { get; private set; }
        public IEnumerable<ICertificateStoreConfiguration> CertificateStores { get; private set; }
        public BasicAuthCredentialProvider BasicAuthCredential { get; private set; }

        public KuduTestConfiguration()
        {
            ApplicationsPath = PathHelper.SitesPath;
            ServiceSitePath = PathHelper.ServiceSitePath;
        }
    }
}

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.SiteManagement;
using Kudu.SiteManagement.Configuration;

namespace Kudu.TestHarness
{
    public static class SitePool
    {
        private const int MaxSiteNameIndex = 5;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(initialCount: 1);
        private static readonly string _sitePrefix = KuduUtils.SiteReusedForAllTests;
        private static readonly ConcurrentStack<int> _availableSiteIndex = new ConcurrentStack<int>(Enumerable.Range(1, MaxSiteNameIndex).Reverse());
        private static ApplicationManager _nextAppManager;

        public static async Task<ApplicationManager> CreateApplicationAsync()
        {
            await _semaphore.WaitAsync();
            ApplicationManager appManager = _nextAppManager;
            _nextAppManager = null;
            _semaphore.Release();

            if (appManager == null)
            {
                appManager = await CreateApplicationInternal();
            }

            EnsureNextApplication();
            return appManager;
        }

        private static async void EnsureNextApplication()
        {
            await AppDomainHelper.RunTask(async () => 
            {
                await _semaphore.WaitAsync();
                try
                {
                    _nextAppManager = await CreateApplicationInternal();
                }
                finally
                {
                    _semaphore.Release();
                }
            });
        }

        public static void ReportTestCompletion(ApplicationManager applicationManager, bool success)
        {
            if (success || _availableSiteIndex.Count <= 1)
            {
                _availableSiteIndex.Push(applicationManager.SitePoolIndex);
            }
            else
            {
                TestTracer.Trace("SitePool.ReportTestCompletion Removing application {0} from pool", applicationManager.ApplicationName);
            }
        }

        private static async Task<ApplicationManager> CreateApplicationInternal()
        {
            int siteIndex;
            _availableSiteIndex.TryPop(out siteIndex);
            string applicationName = _sitePrefix + siteIndex;

            string operationName = "SitePool.CreateApplicationInternal " + applicationName;
            
            var siteManager = GetSiteManager(new KuduTestConfiguration());

            Site site = siteManager.GetSite(applicationName);
            if (site != null)
            {
                TestTracer.Trace("{0} Site already exists at {1}. Reusing site", operationName, site.SiteUrl);
                var appManager = new ApplicationManager(siteManager, site, applicationName)
                {
                    SitePoolIndex = siteIndex
                };

                // In site reuse mode, clean out the existing site so we start clean
                // Enumrate all w3wp processes and make sure to kill any process with an open handle to klr.host.dll
                foreach (var process in (await appManager.ProcessManager.GetProcessesAsync()).Where(p => p.Name.Equals("w3wp", StringComparison.OrdinalIgnoreCase)))
                {
                    var extendedProcess = await appManager.ProcessManager.GetProcessAsync(process.Id);
                    if (extendedProcess.OpenFileHandles.Any(h => h.IndexOf("klr.host.dll", StringComparison.OrdinalIgnoreCase) != -1))
                    {
                        await appManager.ProcessManager.KillProcessAsync(extendedProcess.Id, throwOnError:false);
                    }
                }

                await appManager.RepositoryManager.Delete(deleteWebRoot: true, ignoreErrors: true);

                // Make sure we start with the correct default file as some tests expect it
                WriteIndexHtml(appManager);

                TestTracer.Trace("{0} completed", operationName);
                return appManager;
            }
            TestTracer.Trace("{0} Creating new site", operationName);
            site = await siteManager.CreateSiteAsync(applicationName);

            TestTracer.Trace("{0} Created new site at {1}", operationName, site.SiteUrl);
            return new ApplicationManager(siteManager, site, applicationName)
            {
                SitePoolIndex = siteIndex
            };
        }



        private static ISiteManager GetSiteManager(IKuduConfiguration config)
        {
            //TODO: Mock Searcher.
            return new SiteManager(new PathResolver(config), config, new CertificateSearcher(), true, PathHelper.TestResultsPath);
        }

        // Try to write index.html.  In case of failure with 502, we will include
        // UpTime information with the exception.  This info will be used for furthur
        // investigation of Bad Gateway issue.
        private static void WriteIndexHtml(ApplicationManager appManager)
        {
            try
            {
                appManager.VfsWebRootManager.WriteAllText("hostingstart.html", "<h1>This web site has been successfully created</h1>");
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

    public class KuduTestConfiguration : IKuduConfiguration
    {
        public string RootPath { get; private set; }
        public string ApplicationsPath { get; private set; }
        public string ServiceSitePath { get; private set; }
        public bool CustomHostNamesEnabled { get; private set; }
        public IUrlConfiguration ServiceBase { get; private set; }
        public IUrlConfiguration ApplicationBase { get; private set; }
        public ICertificateConfiguration Certificate { get; private set; }

        public KuduTestConfiguration()
        {
            ApplicationsPath = PathHelper.SitesPath;
            ServiceSitePath = PathHelper.ServiceSitePath;
        }
    }
}

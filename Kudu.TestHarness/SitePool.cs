using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.SiteManagement;

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
            await Task.Run(async () => 
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
            if (success)
            {
                _availableSiteIndex.Push(applicationManager.SitePoolIndex);
            }
            else if (_availableSiteIndex.Count <= 1)
            {
                _availableSiteIndex.Push(applicationManager.SitePoolIndex);
            }
        }

        private static async Task<ApplicationManager> CreateApplicationInternal()
        {
            int siteIndex;
            _availableSiteIndex.TryPop(out siteIndex);
            string applicationName = _sitePrefix + siteIndex;
            var pathResolver = new DefaultPathResolver(PathHelper.ServiceSitePath, PathHelper.SitesPath);
            var settingsResolver = new DefaultSettingsResolver();

            var siteManager = GetSiteManager(pathResolver, settingsResolver);

            Site site = siteManager.GetSite(applicationName);
            if (site != null)
            {
                var appManager = new ApplicationManager(siteManager, site, applicationName, settingsResolver)
                {
                    SitePoolIndex = siteIndex
                };

                // In site reuse mode, clean out the existing site so we start clean
                await appManager.RepositoryManager.Delete(deleteWebRoot: true, ignoreErrors: true);

                // Make sure we start with the correct default file as some tests expect it
                WriteIndexHtml(appManager);

                return appManager;
            }
            else
            {
                site = await siteManager.CreateSiteAsync(applicationName);
                return new ApplicationManager(siteManager, site, applicationName, settingsResolver)
                {
                    SitePoolIndex = siteIndex
                };
            }
        }

        private static ISiteManager GetSiteManager(DefaultPathResolver pathResolver, DefaultSettingsResolver settingsResolver)
        {
            return new SiteManager(pathResolver, traceFailedRequests: true, logPath: PathHelper.TestResultsPath, settingsResolver: settingsResolver);
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
}

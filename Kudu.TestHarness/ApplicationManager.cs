using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Kudu.Client;
using Kudu.Client.Deployment;
using Kudu.Client.Editor;
using Kudu.Client.SourceControl;
using Kudu.Client.SSHKey;
using Kudu.Core.Infrastructure;
using Kudu.SiteManagement;

namespace Kudu.TestHarness
{
    public class ApplicationManager
    {
        private readonly ISiteManager _siteManager;
        private readonly ISettingsResolver _settingsResolver;
        private readonly Site _site;
        private readonly string _appName;

        private ApplicationManager(ISiteManager siteManager, Site site, string appName, string gitUrl, ISettingsResolver settingsResolver)
        {
            _siteManager = siteManager;
            _site = site;
            _appName = appName;
            GitUrl = gitUrl;
            _settingsResolver = settingsResolver;
        }

        public string SiteUrl
        {
            get;
            private set;
        }

        public string ServiceUrl
        {
            get;
            private set;
        }

        public RemoteDeploymentManager DeploymentManager
        {
            get;
            private set;
        }

        public RemoteDeploymentSettingsManager SettingsManager
        {
            get;
            private set;
        }

        public RemoteRepositoryManager RepositoryManager
        {
            get;
            private set;
        }

        public RemoteLogStreamManager LogStreamManager
        {
            get;
            private set;
        }

        public RemoteSSHKeyManager SSHKeyManager
        {
            get;
            private set;
        }

        public RemoteVfsManager VfsManager
        {
            get;
            private set;
        }

        public RemoteVfsManager VfsWebRootManager
        {
            get;
            private set;
        }

        public RemoteVfsManager LiveScmVfsManager
        {
            get;
            private set;
        }

        public string GitUrl
        {
            get;
            private set;
        }

        private void Delete()
        {
            // Don't delete the site if we're supposed to reuse it
            if (!KuduUtils.ReuseSameSiteForAllTests)
            {
                _siteManager.DeleteSite(_appName);
            }
        }

        public void Save(string path, string content)
        {
            string fullPath = Path.Combine(PathHelper.TestResultsPath, _appName, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            File.WriteAllText(fullPath, content);
        }

        public static void Run(string testName, Action<ApplicationManager> action)
        {
            var appManager = CreateApplication(KuduUtils.GetRandomWebsiteName(testName));

            if (KuduUtils.ReuseSameSiteForAllTests)
            {
                // In site reuse mode, clean out the existing site so we start clean
                appManager.RepositoryManager.Delete(deleteWebRoot: true, ignoreErrors: true).Wait();

                // Make sure we start with the correct default file as some tests expect it
                appManager.VfsWebRootManager.WriteAllText("index.html", "<h1>This web site has been successfully created</h1>");
            }

            const string siteBuilderFactory = "site_builder_factory";
            if (KuduUtils.TestOriginalSiteBuilderFactory)
            {
                appManager.SettingsManager.SetValue(siteBuilderFactory, "original").Wait();
            }
            else
            {
                appManager.SettingsManager.Delete(siteBuilderFactory).Wait();
            }

            var dumpPath = Path.Combine(PathHelper.TestResultsPath, testName, testName + ".zip");
            try
            {
                using (StartLogStream(appManager))
                {
                    action(appManager);
                }

                KuduUtils.DownloadDump(appManager.ServiceUrl, dumpPath);
            }
            catch (Exception ex)
            {
                KuduUtils.DownloadDump(appManager.ServiceUrl, dumpPath);

                Debug.WriteLine(ex.Message);

                var httpResponseEx = ex as HttpUnsuccessfulRequestException;
                if (httpResponseEx != null)
                {
                    Debug.WriteLine(httpResponseEx.ResponseMessage);
                }
                throw;
            }
            finally
            {
                SafeTraceDeploymentLogs(appManager);

                // Delete the site at the end, unless we're in site reuse mode
                if (!KuduUtils.ReuseSameSiteForAllTests)
                {
                    appManager.Delete();
                }
            }
        }

        private static IDisposable StartLogStream(ApplicationManager appManager)
        {
            LogStreamWaitHandle waitHandle = null;
            Task task = null;
            if (Debugger.IsAttached)
            {
                // Set to verbose level
                appManager.SettingsManager.SetValue("trace_level", "4").Wait();

                RemoteLogStreamManager mgr = appManager.CreateLogStreamManager("Git");
                waitHandle = new LogStreamWaitHandle(mgr.GetStream().Result);
                task = Task.Factory.StartNew(() =>
                {
                    string line = null;
                    var trace = new DefaultTraceListener();
                    while ((line = waitHandle.WaitNextLine(-1)) != null)
                    {
                        trace.WriteLine(line);
                    }
                });
            }

            return new DisposableAction(() =>
            {
                if (waitHandle != null)
                {
                    waitHandle.Dispose();
                    task.Wait();
                }
            });
        }

        private static void SafeTraceDeploymentLogs(ApplicationManager appManager)
        {
            try
            {
                var results = appManager.DeploymentManager.GetResultsAsync().Result;
                foreach (var result in results)
                {
                    TestTracer.TraceDeploymentLog(appManager, result.Id);
                }
            }
            catch
            {
            }
        }

        public static ApplicationManager CreateApplication(string applicationName)
        {
            var pathResolver = new DefaultPathResolver(PathHelper.ServiceSitePath, PathHelper.SitesPath);
            var settingsResolver = new DefaultSettingsResolver();

            var siteManager = GetSiteManager(pathResolver, settingsResolver);

            Site site;

            if (KuduUtils.ReuseSameSiteForAllTests)
            {
                // In site reuse mode, try to get the existing site, and create it if needed
                site = siteManager.GetSite(applicationName);
                if (site == null)
                {
                    site = siteManager.CreateSite(applicationName);
                }
            }
            else
            {
                try
                {
                    siteManager.DeleteSite(applicationName);
                }
                catch (Exception)
                {

                }

                site = siteManager.CreateSite(applicationName);
            }

            string gitUrl = null;
            var repositoryManager = new RemoteRepositoryManager(site.ServiceUrl + "live/scm");
            var repositoryInfo = repositoryManager.GetRepositoryInfo().Result;
            gitUrl = repositoryInfo.GitUrl.ToString();
            var applicationManager = new ApplicationManager(siteManager, site, applicationName, gitUrl, settingsResolver)
            {
                SiteUrl = site.SiteUrl,
                ServiceUrl = site.ServiceUrl,
                DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "deployments"),
                SettingsManager = new RemoteDeploymentSettingsManager(site.ServiceUrl + "settings"),
                LogStreamManager = new RemoteLogStreamManager(site.ServiceUrl + "logstream"),
                SSHKeyManager = new RemoteSSHKeyManager(site.ServiceUrl + "sshkey"),
                VfsManager = new RemoteVfsManager(site.ServiceUrl + "vfs"),
                VfsWebRootManager = new RemoteVfsManager(site.ServiceUrl + "vfs/site/wwwroot"),
                LiveScmVfsManager = new RemoteVfsManager(site.ServiceUrl + "scmvfs"),
                RepositoryManager = repositoryManager,
            };

            return applicationManager;
        }

        public RemoteLogStreamManager CreateLogStreamManager(string path = null)
        {
            if (path != null)
            {
                path = "/" + path;
            }
            return new RemoteLogStreamManager(_site.ServiceUrl + "logstream" + path);
        }

        private static ISiteManager GetSiteManager(DefaultPathResolver pathResolver, DefaultSettingsResolver settingsResolver)
        {
            return new SiteManager(pathResolver, traceFailedRequests: true, logPath: PathHelper.TestResultsPath, settingsResolver: settingsResolver);
        }
    }
}

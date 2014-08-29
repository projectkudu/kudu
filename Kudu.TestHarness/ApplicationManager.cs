using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Kudu.Client.Command;
using Kudu.Client.Deployment;
using Kudu.Client.Diagnostics;
using Kudu.Client.Editor;
using Kudu.Client.Infrastructure;
using Kudu.Client.Jobs;
using Kudu.Client.SiteExtensions;
using Kudu.Client.SourceControl;
using Kudu.Client.SSHKey;
using Kudu.Core.Infrastructure;
using Kudu.SiteManagement;

namespace Kudu.TestHarness
{
    public class ApplicationManager
    {
        private static bool _testFailureOccured;
        private readonly ISiteManager _siteManager;
        private readonly ISettingsResolver _settingsResolver;
        private readonly Site _site;
        private readonly string _appName;

        internal ApplicationManager(ISiteManager siteManager, Site site, string appName, ISettingsResolver settingsResolver)
        {
            _siteManager = siteManager;
            _site = site;
            _appName = appName;
            _settingsResolver = settingsResolver;

            // Always null in public Kudu, but makes the code more similar to private Kudu
            NetworkCredential credentials = null;

            SiteUrl = site.SiteUrl;
            ServiceUrl = site.ServiceUrl;

            DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "api/deployments", credentials);
            SettingsManager = new RemoteDeploymentSettingsManager(site.ServiceUrl + "api/settings", credentials);
            LegacySettingsManager = new RemoteDeploymentLegacySettingsManager(site.ServiceUrl + "settings", credentials);
            LogStreamManager = new RemoteLogStreamManager(site.ServiceUrl + "api/logstream", credentials);
            SSHKeyManager = new RemoteSSHKeyManager(site.ServiceUrl + "api/sshkey", credentials);
            VfsManager = new RemoteVfsManager(site.ServiceUrl + "api/vfs", credentials);
            VfsWebRootManager = new RemoteVfsManager(site.ServiceUrl + "api/vfs/site/wwwroot", credentials);
            LiveScmVfsManager = new RemoteVfsManager(site.ServiceUrl + "api/scmvfs", credentials);
            ZipManager = new RemoteZipManager(site.ServiceUrl + "api/zip", credentials);
            RuntimeManager = new RemoteRuntimeManager(site.ServiceUrl + "api/diagnostics/runtime", credentials);
            CommandExecutor = new RemoteCommandExecutor(site.ServiceUrl + "api/command", credentials);
            ProcessManager = new RemoteProcessManager(site.ServiceUrl + "api/processes", credentials);
            WebHooksManager = new RemoteWebHooksManager(site.ServiceUrl + "api/hooks", credentials);
            RepositoryManager = new RemoteRepositoryManager(site.ServiceUrl + "api/scm", credentials);
            JobsManager = new RemoteJobsManager(site.ServiceUrl + "api", credentials);
            LogFilesManager = new RemoteLogFilesManager(site.ServiceUrl + "api/logs", credentials);
            SiteExtensionManager = new RemoteSiteExtensionManager(site.ServiceUrl + "api", credentials);

            var repositoryInfo = RepositoryManager.GetRepositoryInfo().Result;
            GitUrl = repositoryInfo.GitUrl.OriginalString;
        }

        public string ApplicationName
        {
            get { return _appName; }
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

        public ISiteManager SiteManager
        {
            get { return _siteManager; }
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

        public RemoteDeploymentLegacySettingsManager LegacySettingsManager
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

        public RemoteZipManager ZipManager
        {
            get;
            private set;
        }

        public RemoteRuntimeManager RuntimeManager
        {
            get;
            private set;
        }

        public RemoteCommandExecutor CommandExecutor
        {
            get;
            private set;
        }

        public RemoteProcessManager ProcessManager
        {
            get;
            private set;
        }

        public RemoteWebHooksManager WebHooksManager
        {
            get;
            private set;
        }

        public RemoteJobsManager JobsManager
        {
            get;
            private set;
        }

        public RemoteLogFilesManager LogFilesManager
        {
            get;
            private set;
        }

        public RemoteSiteExtensionManager SiteExtensionManager
        {
            get;
            private set;
        }

        public string GitUrl
        {
            get;
            private set;
        }

        internal int SitePoolIndex
        {
            get;
            set;
        }

        public string GetCustomGitUrl(string path)
        {
            // Return a custom git url, e.g. http://kuduservice/git/foo/bar
            return GitUrl.Substring(0, GitUrl.LastIndexOf("/")) + "/git/" + path;
        }

        public void Save(string path, string content)
        {
            string fullPath = Path.Combine(PathHelper.TestResultsPath, _appName, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            File.WriteAllText(fullPath, content);
        }

        public string GetKuduUpTime()
        {
            const string pattern = @"<div class=""col-xs-2"">\s*<strong>Site up time</strong>\s*</div>\s*<div>([^<]*)</div>";

            string content = OperationManager.Attempt<string>(() =>
            {
                using (HttpClient client = HttpClientHelper.CreateClient(this.ServiceUrl, this.DeploymentManager.Credentials))
                {
                    using (HttpResponseMessage response = client.GetAsync(String.Empty).Result.EnsureSuccessStatusCode())
                    {
                        return response.Content.ReadAsStringAsync().Result;
                    }
                }
            }, 3, 1000);

            MatchCollection matches = Regex.Matches(content, pattern);
            Debug.Assert(matches.Count == 1, "Could not find Up Time section!");
            Debug.Assert(matches[0].Groups.Count == 2, "Could not find Up Time value!");
            return matches[0].Groups[1].Value;
        }

        public static void Run(string testName, Action<ApplicationManager> action)
        {
            Func<ApplicationManager, Task> asyncAction = (appManager) =>
            {
                action(appManager);
                return Task.FromResult(0);
            };

            RunAsync(testName, asyncAction).Wait();
        }

        public static async Task RunAsync(string testName, Func<ApplicationManager, Task> action)
        {
            if (KuduUtils.StopAfterFirstTestFailure && _testFailureOccured)
            {
                return;
            }

            try
            {
                await RunNoCatch(testName, action);
            }
            catch
            {
                throw;
            }
        }

        public static async Task RunNoCatch(string testName, Func<ApplicationManager, Task> action)
        {
            TestTracer.Trace("Running test - {0}", testName);

            var appManager = await SitePool.CreateApplicationAsync();
            TestTracer.Trace("Using site - {0}", appManager.SiteUrl);

            var dumpPath = Path.Combine(PathHelper.TestResultsPath, testName, testName + ".zip");
            bool succcess = true;
            try
            {
                using (StartLogStream(appManager))
                {
                    await action(appManager);
                }

                KuduUtils.DownloadDump(appManager.ServiceUrl, dumpPath);
            }
            catch (Exception ex)
            {
                KuduUtils.DownloadDump(appManager.ServiceUrl, dumpPath);

                TestTracer.Trace("Run failed with exception\n{0}", ex);

                succcess = false;

                _testFailureOccured = true;

                throw;
            }
            finally
            {
                SafeTraceDeploymentLogs(appManager);

                SitePool.ReportTestCompletion(appManager, succcess);
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

        public RemoteLogStreamManager CreateLogStreamManager(string path = null)
        {
            if (path != null)
            {
                path = "/" + path;
            }
            return new RemoteLogStreamManager(_site.ServiceUrl + "logstream" + path);
        }
    }
}

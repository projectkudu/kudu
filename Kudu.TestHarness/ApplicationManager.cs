using System;
using System.Diagnostics;
using System.IO;
using Kudu.Client;
using Kudu.Client.Deployment;
using Kudu.Client.Editor;
using Kudu.Client.SourceControl;
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

        public RemoteProjectSystem ProjectSystem
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

        public string GitUrl
        {
            get;
            private set;
        }

        private void Delete()
        {
            _siteManager.DeleteSite(_appName);
        }

        public void Save(string path, string content)
        {
            string fullPath = Path.Combine(PathHelper.TestResultsPath, _appName, path);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));

            File.WriteAllText(fullPath, content);
        }

        public static void Run(string applicationName, Action<ApplicationManager> action)
        {
            var appManager = CreateApplication(applicationName);
            var dumpPath = Path.Combine(PathHelper.TestResultsPath, applicationName, applicationName + ".zip");
            try
            {
                action(appManager);

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
                appManager.Delete();
            }
        }

        public static ApplicationManager CreateApplication(string applicationName)
        {
            var pathResolver = new DefaultPathResolver(PathHelper.ServiceSitePath, PathHelper.SitesPath);
            var settingsResolver = new DefaultSettingsResolver(".example.org");

            var siteManager = GetSiteManager(pathResolver, settingsResolver);

            try
            {
                siteManager.DeleteSite(applicationName);
            }
            catch (Exception)
            {

            }

            Site site = siteManager.CreateSite(applicationName);

            string gitUrl = null;
            var repositoryManager = new RemoteRepositoryManager(site.ServiceUrl + "live/scm");
            var repositoryInfo = repositoryManager.GetRepositoryInfo().Result;
            gitUrl = repositoryInfo.GitUrl.ToString();
            return new ApplicationManager(siteManager, site, applicationName, gitUrl, settingsResolver)
            {
                SiteUrl = site.SiteUrl,
                ServiceUrl = site.ServiceUrl,
                DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "deployments"),
                ProjectSystem = new RemoteProjectSystem(site.ServiceUrl + "live/files"),
                SettingsManager = new RemoteDeploymentSettingsManager(site.ServiceUrl + "settings"),
                LogStreamManager = new RemoteLogStreamManager(site.ServiceUrl + "logstream"),

                RepositoryManager = repositoryManager
            };
        }

        public RemoteLogStreamManager CreateLogStreamManager(string path = null)
        {
            if (path != null)
            {
                path = "/" + path;
            }
            return new RemoteLogStreamManager(_site.ServiceUrl + "logstream" + path);
        }

        private static SiteManager GetSiteManager(DefaultPathResolver pathResolver, DefaultSettingsResolver settingsResolver)
        {
            return new SiteManager(pathResolver, traceFailedRequests: true, logPath: PathHelper.TestResultsPath, settingsResolver: settingsResolver);
        }
    }
}

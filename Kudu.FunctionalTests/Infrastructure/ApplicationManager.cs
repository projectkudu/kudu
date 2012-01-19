using System;
using System.IO;
using Kudu.Client.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;

namespace Kudu.FunctionalTests.Infrastructure
{
    public class ApplicationManager : IDisposable
    {
        private readonly ISiteManager _siteManager;
        private readonly Site _site;
        private readonly string _appName;
        private readonly string _path;

        private ApplicationManager(ISiteManager siteManager, Site site, string appName, string path)
        {
            _siteManager = siteManager;
            _site = site;
            _appName = appName;
            _path = path;
        }

        public string SiteUrl
        {
            get;
            private set;
        }

        public string RepositoryPath
        {
            get
            {
                return Path.Combine(PathHelper.SitesPath, _appName, @"live\repository");
            }
        }

        public RemoteDeploymentManager DeploymentManager
        {
            get;
            private set;
        }

        public string GitUrl
        {
            get
            {
                return GetCloneUrl(_site, RepositoryType.Git);
            }
        }

        public string HgUrl
        {
            get
            {
                return GetCloneUrl(_site, RepositoryType.Mercurial);
            }
        }

        void IDisposable.Dispose()
        {
            CopyLogs();

            _siteManager.DeleteSite(_appName);
        }

        private void CopyLogs()
        {
            try
            {
                string targetPath = Path.Combine(PathHelper.TestResultsPath, _appName);

                // Clear the old logs
                FileSystemHelpers.DeleteDirectorySafe(targetPath);

                string[] logPaths = new[] { "profiles", "deployments" };


                foreach (var logPath in logPaths)
                {
                    string source = Path.Combine(_path, logPath);
                    string dest = Path.Combine(targetPath, logPath);
                    FileSystemHelpers.Copy(source, dest);
                }
            }
            catch
            {
                // Swallow this exception
            }
        }

        public static ApplicationManager CreateApplication(string applicationName)
        {
            var pathResolver = new DefaultPathResolver(PathHelper.ServiceSitePath, PathHelper.SitesPath);
            var siteManager = new SiteManager(pathResolver);
            Site site = siteManager.CreateSite(applicationName);

            return new ApplicationManager(siteManager, site, applicationName, pathResolver.GetApplicationPath(applicationName))
            {
                SiteUrl = site.SiteUrl,
                DeploymentManager = new RemoteDeploymentManager(site.ServiceUrl + "deploy")
            };
        }

        private string GetCloneUrl(Site site, RepositoryType type)
        {
            return site.ServiceUrl + (type == RepositoryType.Git ? "git" : "hg");
        }
    }
}

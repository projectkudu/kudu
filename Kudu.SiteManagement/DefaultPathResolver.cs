using System.IO;

namespace Kudu.SiteManagement
{
    public class DefaultPathResolver : IPathResolver
    {
        private readonly string _rootPath;
        private readonly string _serviceSitePath;

        public DefaultPathResolver(string serviceSitePath, string rootPath)
        {
            _serviceSitePath = Path.GetFullPath(serviceSitePath);
            _rootPath = Path.GetFullPath(rootPath);
        }

        public string ServiceSitePath
        {
            get
            {
                return _serviceSitePath;
            }
        }

        public string SitesPath
        {
            get
            {
                return _rootPath;
            }
        }

        public string GetApplicationPath(string applicationName)
        {
            return Path.Combine(_rootPath, applicationName);
        }

        public string GetLiveSitePath(string applicationName)
        {
            return Path.Combine(GetApplicationPath(applicationName), "live");
        }

        public string GetDeveloperApplicationPath(string applicationName)
        {
            return Path.Combine(GetApplicationPath(applicationName), "dev");
        }
    }
}

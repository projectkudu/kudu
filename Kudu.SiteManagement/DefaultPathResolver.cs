using System;
using System.IO;
using Kudu.SiteManagement.Configuration;

namespace Kudu.SiteManagement
{
    public class DefaultPathResolver : IPathResolver
    {
        private const string DefaultKuduServicePath = @"%SystemDrive%\KuduService\wwwroot";

        private readonly string _rootPath;
        private readonly string _fallbackServiceSitePath;

        public DefaultPathResolver(string fallbackServiceSitePath, string rootPath)
        {
            _fallbackServiceSitePath = Path.GetFullPath(fallbackServiceSitePath);
            _rootPath = Path.GetFullPath(rootPath);
        }

        public string ServiceSitePath
        {
            get
            {
                // If the default path to the kudu service exists then use it
                // Q: Why are we prioritizing a default value over a configured value? o.O
                string path = Environment.ExpandEnvironmentVariables(DefaultKuduServicePath);
                if (Directory.Exists(path))
                {
                    return path;
                }
                
                return _fallbackServiceSitePath;
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
            return Path.Combine(GetApplicationPath(applicationName), Constants.SiteFolder);
        }
    }
}

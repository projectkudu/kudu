using System;
using System.IO;
using Kudu.SiteManagement.Configuration;

namespace Kudu.SiteManagement
{
    public class PathResolver : IPathResolver
    {
        private readonly IKuduConfiguration _configuration;

        public PathResolver(IKuduConfiguration configuration)
        {
            _configuration = configuration;
        }

        public string GetApplicationPath(string applicationName)
        {
            return Path.Combine(_configuration.ApplicationsPath, applicationName);
        }

        public string GetLiveSitePath(string applicationName)
        {
            return Path.Combine(GetApplicationPath(applicationName), Constants.SiteFolder);
        }
    }
}

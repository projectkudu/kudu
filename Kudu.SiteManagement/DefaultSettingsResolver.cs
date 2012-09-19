using System;
using System.IO;

namespace Kudu.SiteManagement
{
    public class DefaultSettingsResolver : ISettingsResolver
    {
        private readonly string _sitesBaseUrl;

        public DefaultSettingsResolver(string sitesBaseUrl)
        {
            _sitesBaseUrl = sitesBaseUrl;
        }

        public string SitesBaseUrl
        {
            get
            {
                return _sitesBaseUrl;
            }
        }
    }
}

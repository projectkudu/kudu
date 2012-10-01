using System;
using System.IO;

namespace Kudu.SiteManagement
{
    public class DefaultSettingsResolver : ISettingsResolver
    {
        private readonly string _sitesBaseUrl;

        public DefaultSettingsResolver(string sitesBaseUrl)
        {
            // Ensure the base url is normalised to not have a leading dot,
            // we will add this on later when joining the application name up
            _sitesBaseUrl = sitesBaseUrl.TrimStart(new [] { '.' });
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

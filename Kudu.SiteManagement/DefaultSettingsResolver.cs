using System;
using System.IO;

namespace Kudu.SiteManagement
{
    public class DefaultSettingsResolver : ISettingsResolver
    {
        private readonly string _sitesBaseUrl;

        public DefaultSettingsResolver()
            : this(sitesBaseUrl: null)
        {
        }

        public DefaultSettingsResolver(string sitesBaseUrl)
        {
            // Ensure the base url is normalised to not have a leading dot,
            // we will add this on later when joining the application name up
            if (sitesBaseUrl != null)
            {
                _sitesBaseUrl = sitesBaseUrl.TrimStart('.');
            }
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

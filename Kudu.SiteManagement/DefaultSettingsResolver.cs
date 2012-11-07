using System;
using System.IO;

namespace Kudu.SiteManagement
{
    public class DefaultSettingsResolver : ISettingsResolver
    {
        private readonly string _sitesBaseUrl;
        private readonly string _serviceSitesBaseUrl;

        public DefaultSettingsResolver()
            : this(sitesBaseUrl: null, serviceSitesBaseUrl: null)
        {
        }

        public DefaultSettingsResolver(string sitesBaseUrl, string serviceSitesBaseUrl)
        {
            // Ensure the base url is normalised to not have a leading dot,
            // we will add this on later when joining the application name up
            if (sitesBaseUrl != null)
            {
                _sitesBaseUrl = sitesBaseUrl.TrimStart('.');
            }
            if (serviceSitesBaseUrl != null)
            {
                _serviceSitesBaseUrl = serviceSitesBaseUrl.TrimStart('.');
            }
        }

        public string SitesBaseUrl
        {
            get
            {
                return _sitesBaseUrl;
            }
        }

        public string ServiceSitesBaseUrl
        {
            get
            {
                return _serviceSitesBaseUrl;
            }            
        }
    }
}

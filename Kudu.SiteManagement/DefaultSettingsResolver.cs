using System;
using System.Configuration;
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

            if (!String.IsNullOrEmpty(_serviceSitesBaseUrl) && !String.IsNullOrEmpty(_sitesBaseUrl))
            {
                if (_serviceSitesBaseUrl.Equals(_sitesBaseUrl, StringComparison.OrdinalIgnoreCase))
                {
                    throw new ArgumentException("serviceSitesBaseUrl cannot be the same as sitesBaseUrl.");
                }
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

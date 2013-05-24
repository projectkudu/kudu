using System;
using System.Configuration;
using System.IO;

namespace Kudu.SiteManagement
{
    public class DefaultSettingsResolver : ISettingsResolver
    {
        private readonly string _sitesBaseUrl;
        private readonly string _serviceSitesBaseUrl;
        private readonly bool _customHostNames;

        public DefaultSettingsResolver()
            : this(sitesBaseUrl: null, serviceSitesBaseUrl: null, enableCustomHostNames: null)
        {
        }

        public DefaultSettingsResolver(string sitesBaseUrl, string serviceSitesBaseUrl, string enableCustomHostNames)
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

            if (enableCustomHostNames == null || !Boolean.TryParse(enableCustomHostNames, out _customHostNames))
            {
                _customHostNames = false;
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

        public bool CustomHostNames
        {
            get
            {
                return _customHostNames;
            }
        }
    }
}

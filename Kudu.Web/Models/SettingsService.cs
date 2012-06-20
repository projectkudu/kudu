using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Kudu.SiteManagement;
using Kudu.Web.Infrastructure;

namespace Kudu.Web.Models
{
    public class SettingsService : ISettingsService
    {
        private readonly IApplicationService _applicationService;
        private readonly ICredentialProvider _credentialProvider;
        private readonly ISiteManager _siteManager;

        public SettingsService(IApplicationService applicationService, ICredentialProvider credentialProvider, ISiteManager siteManager)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
            _siteManager = siteManager;
        }

        public ISettings GetSettings(string siteName)
        {
            var site = _siteManager.GetSite(siteName);
            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentSettingsManager settingsManager = site.GetSettingsManager(credentials);

            return new Settings
            {
                AppSettings = Convert(settingsManager.GetAppSettings()),
                ConnectionStrings = Convert(settingsManager.GetConnectionStrings())
            };
        }

        public void SetConnectionString(string siteName, string name, string connectionString)
        {
            var site = _siteManager.GetSite(siteName);
            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentSettingsManager settingsManager = site.GetSettingsManager(credentials);

            settingsManager.SetConnectionString(name, connectionString);
        }

        public void RemoveConnectionString(string siteName, string name)
        {
            var site = _siteManager.GetSite(siteName);
            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentSettingsManager settingsManager = site.GetSettingsManager(credentials);

            settingsManager.RemoveConnectionString(name);
        }

        public void RemoveAppSetting(string siteName, string key)
        {
            var site = _siteManager.GetSite(siteName);
            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentSettingsManager settingsManager = site.GetSettingsManager(credentials);

            settingsManager.RemoveAppSetting(key);
        }

        public void SetAppSetting(string siteName, string key, string value)
        {
            var site = _siteManager.GetSite(siteName);
            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentSettingsManager settingsManager = site.GetSettingsManager(credentials);

            settingsManager.SetAppSetting(key, value);
        }

        private NameValueCollection Convert(IEnumerable<DeploymentSetting> appSettings)
        {
            var nvc = new NameValueCollection();
            foreach (var setting in appSettings)
            {
                nvc[setting.Key] = setting.Value;
            }
            return nvc;
        }

        private NameValueCollection Convert(IEnumerable<ConnectionStringSetting> connectionStrings)
        {
            var nvc = new NameValueCollection();
            foreach (var conn in connectionStrings)
            {
                nvc[conn.Name] = conn.ConnectionString;
            }
            return nvc;
        }
    }
}
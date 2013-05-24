using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client.Deployment;
using Kudu.Client.Infrastructure;
using Kudu.Web.Infrastructure;

namespace Kudu.Web.Models
{
    public class SettingsService : ISettingsService
    {
        private readonly IApplicationService _applicationService;
        private readonly ICredentialProvider _credentialProvider;

        public SettingsService(IApplicationService applicationService, ICredentialProvider credentialProvider)
        {
            _applicationService = applicationService;
            _credentialProvider = credentialProvider;
        }

        public async Task<ISettings> GetSettings(string siteName)
        {
            var settingsManager = GetSettingsManager(siteName);
            NameValueCollection values = await settingsManager.GetValues();
            return new Settings { KuduSettings = values };
        }

        public void SetConnectionString(string siteName, string name, string connectionString)
        {
            // Not supported
        }

        public void RemoveConnectionString(string siteName, string name)
        {
            // Not supported
        }

        public void RemoveAppSetting(string siteName, string key)
        {
            // Not supported 
        }

        public void SetAppSetting(string siteName, string key, string value)
        {
            // Not supported
        }

        public Task SetKuduSetting(string siteName, string key, string value)
        {
            return GetSettingsManager(siteName).SetValue(key, value);
        }

        public Task SetKuduSettings(string siteName, params KeyValuePair<string, string>[] settings)
        {
            return GetSettingsManager(siteName).SetValues(settings);
        }

        public Task RemoveKuduSetting(string siteName, string key)
        {
            return GetSettingsManager(siteName).Delete(key);
        }

        protected RemoteDeploymentSettingsManager GetSettingsManager(string siteName)
        {
            IApplication application = _applicationService.GetApplication(siteName);
            ICredentials credentials = _credentialProvider.GetCredentials();
            RemoteDeploymentSettingsManager settingsManager = application.GetSettingsManager(credentials);
            return settingsManager;
        }
    }
}
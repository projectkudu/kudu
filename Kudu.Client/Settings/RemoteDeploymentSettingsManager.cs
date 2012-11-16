using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentSettingsManager : KuduRemoteClientBase
    {
        public RemoteDeploymentSettingsManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public RemoteDeploymentSettingsManager(string serviceUrl, HttpClientHandler handler)
            : base(serviceUrl, handler)
        {
        }

        public Task SetValueLegacy(string key, string value)
        {
            var values = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("key", key), new KeyValuePair<string, string>("value", value));
            return _client.PostAsync(String.Empty, values).Then(response => response.EnsureSuccessful());
        }

        public Task SetValue(string key, string value)
        {
            var values = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>(key, value));
            return _client.PostAsync(String.Empty, values).Then(response => response.EnsureSuccessful());
        }

        public Task SetValues(params KeyValuePair<string, string>[] values)
        {
            var jsonvalues = HttpClientHelper.CreateJsonContent(values);
            return _client.PostAsync(String.Empty, jsonvalues).Then(response => response.EnsureSuccessful());
        }

        public Task<NameValueCollection> GetValues()
        {
            return _client.GetJsonAsync<JObject>(String.Empty).Then(obj =>
            {
                var nvc = new NameValueCollection();
                foreach (var pair in obj)
                {
                    nvc[pair.Key] = pair.Value.Value<string>();
                }

                return nvc;
            });
        }

        public Task<string> GetValue(string key)
        {
            return _client.GetJsonAsync<string>(key);
        }

        public Task Delete(string key)
        {
            return _client.DeleteSafeAsync(key);
        }
    }
}
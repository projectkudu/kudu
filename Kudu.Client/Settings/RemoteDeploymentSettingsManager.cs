using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentSettingsManager : KuduRemoteClientBase
    {
        public RemoteDeploymentSettingsManager(string serviceUrl, ICredentials credentials = null)
            : base(serviceUrl, credentials)
        {
        }

        public Task SetValueLegacy(string key, string value)
        {
            var values = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("key", key), new KeyValuePair<string, string>("value", value));
            return Client.PostAsync(String.Empty, values).Then(response => response.EnsureSuccessful());
        }

        public Task SetValue(string key, string value)
        {
            var values = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>(key, value));
            return Client.PostAsync(String.Empty, values).Then(response => response.EnsureSuccessful());
        }

        public Task SetValues(params KeyValuePair<string, string>[] values)
        {
            var jsonvalues = HttpClientHelper.CreateJsonContent(values);
            return Client.PostAsync(String.Empty, jsonvalues).Then(response => response.EnsureSuccessful());
        }

        public Task<NameValueCollection> GetValuesLegacy()
        {
            return Client.GetJsonAsync<JArray>(String.Empty).Then(obj =>
            {
                var nvc = new NameValueCollection();
                foreach (JObject value in obj)
                {
                    nvc[value["Key"].Value<string>()] = value["Value"].Value<string>();
                }

                return nvc;
            });
        }

        public Task<NameValueCollection> GetValues()
        {
            return Client.GetJsonAsync<JObject>("?version=2").Then(obj =>
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
            return Client.GetJsonAsync<string>(key);
        }

        public Task Delete(string key)
        {
            return Client.DeleteSafeAsync(key);
        }
    }
}
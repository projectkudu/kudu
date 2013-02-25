using Kudu.Client.Infrastructure;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentSettingsManager : KuduRemoteClientBase
    {
        public RemoteDeploymentSettingsManager(string serviceUrl, ICredentials credentials = null)
            : base(serviceUrl, credentials)
        {
        }

        public async Task SetValueLegacy(string key, string value)
        {
            using (var values = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("key", key), new KeyValuePair<string, string>("value", value)))
            {
                HttpResponseMessage response = await Client.PostAsync(String.Empty, values);
                response.EnsureSuccessful();
            }
        }

        public async Task SetValue(string key, string value)
        {
            using (var values = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>(key, value)))
            {
                HttpResponseMessage response = await Client.PostAsync(String.Empty, values);
                response.EnsureSuccessful();
            }
        }

        public async Task SetValues(params KeyValuePair<string, string>[] values)
        {
            using (var jsonvalues = HttpClientHelper.CreateJsonContent(values))
            {
                HttpResponseMessage response = await Client.PostAsync(String.Empty, jsonvalues);
                response.EnsureSuccessful();
            }
        }

        public async Task<NameValueCollection> GetValuesLegacy()
        {
            var obj = await Client.GetJsonAsync<JArray>(String.Empty);

            var nvc = new NameValueCollection();
            foreach (JObject value in obj)
            {
                nvc[value["Key"].Value<string>()] = value["Value"].Value<string>();
            }

            return nvc;
        }

        public async Task<NameValueCollection> GetValues()
        {
            var obj = await Client.GetJsonAsync<JObject>("?version=2");

            var nvc = new NameValueCollection();
            foreach (var pair in obj)
            {
                nvc[pair.Key] = pair.Value.Value<string>();
            }

            return nvc;
        }

        public async Task<string> GetValue(string key)
        {
            return await Client.GetJsonAsync<string>(key);
        }

        public async Task Delete(string key)
        {
            await Client.DeleteSafeAsync(key);
        }
    }
}
using System.Collections.Generic;
using System.Collections.Specialized;
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

        public Task SetValue(string key, string value)
        {
            var values = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("key", key), new KeyValuePair<string, string>("value", value));
            return _client.PostAsync("/settings", values);
        }

        public Task<NameValueCollection> GetValues()
        {
            return _client.GetJsonAsync<JObject>("/settings").Then(obj =>
            {
                var nvc = new NameValueCollection();
                foreach (var kvp in obj)
                {
                    nvc[kvp.Key] = kvp.Value.Value<string>();
                }

                return nvc;
            }); 
         }

        public Task<string> GetValue(string key)
        {
            return _client.GetJsonAsync<string>("/settings/" + key);
        }

        public Task Delete(string key)
        {
            return _client.DeleteAsync("/settings/" + key).Then(response =>
            {
                return response.EnsureSuccessStatusCode();
            });
        }
    }
}
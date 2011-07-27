using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment {
    public class RemoteDeploymentManager : IDeploymentManager {
        private readonly HttpClient _client;

        public RemoteDeploymentManager(string serviceUrl) {
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public IEnumerable<DeployResult> GetResults() {
            return _client.GetJson<IEnumerable<DeployResult>>("log");
        }

        public DeployResult GetResult(string id) {
            return _client.GetJson<DeployResult>("details?id=" + id);
        }

        public string GetLog(string id) {
            return _client.Get("log?id=" + id).EnsureSuccessful().Content.ReadAsString();
        }

        public void Deploy(string id) {
            _client.Post(String.Empty, new FormUrlEncodedContent(new Dictionary<string, string> {
                { "id", id }
            })).EnsureSuccessful();
        }

        public void Deploy() {
            _client.Post(String.Empty, new StringContent(null)).EnsureSuccessful();
        }
    }
}

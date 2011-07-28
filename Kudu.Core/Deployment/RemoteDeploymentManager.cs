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

        public IEnumerable<LogEntry> GetLogEntries(string id) {
            return _client.GetJson<IEnumerable<LogEntry>>("log?id=" + id);
        }

        public void Deploy(string id) {
            _client.Post(String.Empty, new FormUrlEncodedContent(new Dictionary<string, string> {
                { "id", id }
            })).EnsureSuccessful();
        }

        public void Deploy() {
            _client.Post(String.Empty, new StringContent(String.Empty)).EnsureSuccessful();
        }

        public void Build(string id) {
            _client.Post("create", new StringContent(String.Empty)).EnsureSuccessful();
        }
    }
}

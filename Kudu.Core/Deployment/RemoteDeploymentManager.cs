using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net.Http;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment {
    public class RemoteDeploymentManager : IDeploymentManager, IDeployer {
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

        public void Deploy(string id) {
            _client.Post(String.Empty, new FormUrlEncodedContent(new Dictionary<string, string> {
                { "id", id }
            })).EnsureSuccessful();
        }
    }
}

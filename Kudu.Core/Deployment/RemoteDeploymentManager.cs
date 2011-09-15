using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json;
using SignalR.Client;

namespace Kudu.Core.Deployment {
    public class RemoteDeploymentManager : IDeploymentManager {
        private readonly HttpClient _client;

        public event Action<DeployResult> StatusChanged;

        public RemoteDeploymentManager(string serviceUrl) {
            serviceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            _client = HttpClientHelper.Create(serviceUrl);

            // Raise the event when data comes in
            var connection = new Connection(serviceUrl + "status");
            connection.Received += data => {
                if (StatusChanged != null) {
                    var result = JsonConvert.DeserializeObject<DeployResult>(data);
                    StatusChanged(result);
                }
            };

            connection.Closed += () => {
                Debug.WriteLine("SignalR connection to {0} was closed.", serviceUrl);
            };

            connection.Start().Wait();
        }

        public string ActiveDeploymentId {
            get {
                return _client.Get("id").EnsureSuccessful()
                              .Content
                              .ReadAsString();
            }
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
            _client.Post("new", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "id", id }
            })).EnsureSuccessful();
        }
    }
}

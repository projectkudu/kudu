using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Newtonsoft.Json;
using SignalR.Client;

namespace Kudu.Client.Deployment {
    public class RemoteDeploymentManager : IDeploymentManager {
        private readonly HttpClient _client;
        private readonly Connection _connection;

        public event Action<DeployResult> StatusChanged;
        

        public RemoteDeploymentManager(string serviceUrl) {
            serviceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            _client = HttpClientHelper.Create(serviceUrl);

            // Raise the event when data comes in
            _connection = new Connection(serviceUrl + "status");
            _connection.Received += data => {
                if (StatusChanged != null) {
                    var result = JsonConvert.DeserializeObject<DeployResult>(data);
                    StatusChanged(result);
                }
            };

            _connection.Error += exception => {
                // If we get a 404 back stop listening for changes
                WebException webException = exception as WebException;
                if (webException != null) {
                    var webResponse = (HttpWebResponse)webException.Response;
                    if (webResponse != null && 
                        webResponse.StatusCode == HttpStatusCode.NotFound) {
                        _connection.Stop();
                    }
                }
            };

            _connection.Closed += () => {
                Debug.WriteLine("SignalR connection to {0} was closed.", serviceUrl);
            };

            _connection.Start().Wait();
        }

        public bool IsActive {
            get {
                return _connection.IsActive;
            }
        }

        public string ActiveDeploymentId {
            get {
                return _client.GetJson<string>("id");
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
            _client.Post("restore", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, object>("id", id)))
                   .EnsureSuccessful();
        }

        public void Deploy() {
            _client.Post(String.Empty, new StringContent(String.Empty)).EnsureSuccessful();
        }

        public void Build(string id) {
            _client.Post("build", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, object>("id", id)))
                   .EnsureSuccessful();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Newtonsoft.Json;
using SignalR.Client;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentManager : KuduRemoteClientBase, IDeploymentManager, IEventProvider
    {
        private Connection _connection;

        public event Action<DeployResult> StatusChanged;

        public RemoteDeploymentManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public bool IsActive
        {
            get
            {
                return _connection != null && _connection.IsActive;
            }
        }

        public string ActiveDeploymentId
        {
            get
            {
                return _client.GetJson<string>("id");
            }
        }

        public IEnumerable<DeployResult> GetResults()
        {
            return _client.GetJson<IEnumerable<DeployResult>>("log");
        }

        public Task<IEnumerable<DeployResult>> GetResultsAsync()
        {
            return _client.GetJsonAsync<IEnumerable<DeployResult>>("log");
        }

        public DeployResult GetResult(string id)
        {
            return _client.GetJson<DeployResult>("details?id=" + id);
        }

        public Task<DeployResult> GetResultAsync(string id)
        {
            return _client.GetJsonAsync<DeployResult>("details?id=" + id);
        }

        public IEnumerable<string> GetManifest(string id)
        {
            return _client.GetJson<IEnumerable<string>>("manifest?id=" + id);
        }

        public Task<IEnumerable<string>> GetManifestAsync(string id)
        {
            return _client.GetJsonAsync<IEnumerable<string>>("manifest?id=" + id);
        }

        public IEnumerable<LogEntry> GetLogEntries(string id)
        {
            return _client.GetJson<IEnumerable<LogEntry>>("log?id=" + id);
        }

        public Task<IEnumerable<LogEntry>> GetLogEntriesAsync(string id)
        {
            return _client.GetJsonAsync<IEnumerable<LogEntry>>("log?id=" + id);
        }

        public IEnumerable<LogEntry> GetLogEntryDetails(string id, string entryId)
        {
            return _client.GetJson<IEnumerable<LogEntry>>("logDetails?id=" + id + "&entryId=" + entryId);
        }

        public Task<IEnumerable<LogEntry>> GetLogEntryDetailsAsync(string id, string entryId)
        {
            return _client.GetJsonAsync<IEnumerable<LogEntry>>("logDetails?id=" + id + "&entryId=" + entryId);
        }

        public void Delete(string id)
        {
            _client.PostAsync("delete", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .Result
                   .EnsureSuccessful();
        }

        public Task DeleteAsync(string id)
        {
            return _client.PostAsync("delete", new KeyValuePair<string, string>("id", id));
        }

        public void Deploy(string id)
        {
            _client.PostAsync("restore", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .Result
                   .EnsureSuccessful();
        }

        public Task DeployAsync(string id)
        {
            return _client.PostAsync("restore", new KeyValuePair<string, string>("id", id));
        }

        public void Deploy()
        {
            _client.PostAsync(String.Empty, new StringContent(String.Empty))
                   .Result
                   .EnsureSuccessful();
        }

        public Task DeployAsync()
        {
            return _client.PostAsync();
        }

        public void Build(string id)
        {
            _client.PostAsync("build", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .Result
                   .EnsureSuccessful();
        }

        public Task BuildAsync(string id)
        {
            return _client.PostAsync("build", new KeyValuePair<string, string>("id", id));
        }

        public void Start()
        {
            if (_connection == null)
            {
                // Raise the event when data comes in
                _connection = new Connection(ServiceUrl + "status");
                _connection.Credentials = Credentials;
                _connection.Received += OnReceived;
                _connection.Error += (e) =>
                {
                    Debug.WriteLine("Error: " + e.Message);
                };
            }

            TryStart(retries: 5);
        }

        private void TryStart(int retries)
        {
            if (retries <= 0)
            {
                return;
            }

            if (_connection.MessageId == null)
            {
                // We never want to miss messages if we're just connecting
                // This is for tests
                _connection.MessageId = 0;
            }

            // REVIEW: Should this return task?
            _connection.Start().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.WriteLine("KUDU ERROR: " + t.Exception.GetBaseException());

                    Stop();

                    // Sleep for a second and retry
                    Thread.Sleep(1000);

                    TryStart(retries - 1);
                }
                else
                {
                    Debug.WriteLine("Success!");
                }
            });
        }

        public void Stop()
        {
            if (_connection != null)
            {
                _connection.Stop();
            }
        }

        private void OnReceived(string data)
        {
            if (StatusChanged != null)
            {
                var result = JsonConvert.DeserializeObject<DeployResult>(data);
                StatusChanged(result);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.Deployment;
using Newtonsoft.Json;
using SignalR.Client;

namespace Kudu.Client.Deployment
{
    public class RemoteDeploymentManager : KuduRemoteClientBase
    {
        private Connection _connection;
        private Action<DeployResult> _statusChanged;
        private long _connections;

        public event Action<DeployResult> StatusChanged
        {
            add
            {
                if (Interlocked.Increment(ref _connections) == 1)
                {
                    Start();
                }

                _statusChanged += value;                
            }
            remove
            {
                _statusChanged -= value;

                if (Interlocked.Decrement(ref _connections) == 0)
                {
                    Stop();
                }
            }
        }

        public RemoteDeploymentManager(string serviceUrl)
            : base(serviceUrl)
        {
        }

        public Task<IEnumerable<DeployResult>> GetResultsAsync(int? maxItems = null, bool excludeFailed = false)
        {
            string url = "?$orderby=ReceivedTime desc";
            if (maxItems != null && maxItems >= 0)
            {
                url += String.Format("&$top={0}", maxItems);
            }
            if (excludeFailed)
            {
                url += String.Format("&$filter=Status ne {0}", (int)DeployStatus.Failed);
            }

            return _client.GetJsonAsync<IEnumerable<DeployResult>>(url);
        }

        public Task<DeployResult> GetResultAsync(string id)
        {
            return _client.GetJsonAsync<DeployResult>(id);
        }

        public Task<IEnumerable<LogEntry>> GetLogEntriesAsync(string id)
        {
            return _client.GetJsonAsync<IEnumerable<LogEntry>>(id + "/log");
        }

        public Task<IEnumerable<LogEntry>> GetLogEntryDetailsAsync(string id, string logId)
        {
            return _client.GetJsonAsync<IEnumerable<LogEntry>>(id + "/log/" + logId);
        }

        public Task DeleteAsync(string id)
        {
            return _client.DeleteSafeAsync(id);
        }

        public Task DeployAsync(string id)
        {
            return _client.PutAsync(id);
        }

        public Task DeployAsync(string id, bool clean)
        {
            var param = new KeyValuePair<string, string>("clean", clean.ToString());
            return _client.PutAsync(id, param);
        }

        private void Start()
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
            });
        }

        private void Stop()
        {
            if (_connection != null)
            {
                _connection.Stop();
            }
        }

        private void OnReceived(string data)
        {
            if (_statusChanged != null)
            {
                var result = JsonConvert.DeserializeObject<DeployResult>(data);
                _statusChanged(result);
            }
        }
    }
}

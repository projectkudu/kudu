using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
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
                return _client.GetAsyncJson<string>("id");
            }
        }

        public IEnumerable<DeployResult> GetResults()
        {
            return _client.GetAsyncJson<IEnumerable<DeployResult>>("log");
        }

        public DeployResult GetResult(string id)
        {
            return _client.GetAsyncJson<DeployResult>("details?id=" + id);
        }

        public IEnumerable<LogEntry> GetLogEntries(string id)
        {
            return _client.GetAsyncJson<IEnumerable<LogEntry>>("log?id=" + id);
        }

        public IEnumerable<LogEntry> GetLogEntryDetails(string id, string entryId)
        {
            return _client.GetAsyncJson<IEnumerable<LogEntry>>("logDetails?id=" + id + "&entryId=" + entryId);
        }

        public void Delete(string id)
        {
            _client.PostAsync("delete", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .Result
                   .EnsureSuccessful();
        }

        public void Deploy(string id)
        {
            _client.PostAsync("restore", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .Result
                   .EnsureSuccessful();
        }

        public void Deploy()
        {
            _client.PostAsync(String.Empty, new StringContent(String.Empty))
                   .Result
                   .EnsureSuccessful();
        }

        public void Build(string id)
        {
            _client.PostAsync("build", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .Result
                   .EnsureSuccessful();
        }

        public void Start()
        {
            if (_connection == null)
            {
                // Raise the event when data comes in
                _connection = new Connection(ServiceUrl + "status");
                _connection.Credentials = Credentials;
                _connection.Received += OnReceived;
                _connection.Error += OnError;
            }

            // REVIEW: Should this return task?
            _connection.Start().ContinueWith(t =>
            {
                if (t.IsFaulted)
                {
                    Debug.WriteLine("KUDU ERROR: " + t.Exception.GetBaseException());
                }
            });
        }

        public void Stop()
        {
            if (_connection != null)
            {
                _connection.Received -= OnReceived;
                _connection.Error -= OnError;
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

        private void OnError(Exception exception)
        {
            // If we get a 404 back stop listening for changes
            var webException = exception as WebException;
            if (webException != null)
            {
                var webResponse = (HttpWebResponse)webException.Response;
                if (webResponse != null &&
                    webResponse.StatusCode == HttpStatusCode.NotFound)
                {
                    Stop();
                }
            }
        }
    }
}

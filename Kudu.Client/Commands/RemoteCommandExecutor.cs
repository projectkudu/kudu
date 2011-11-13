using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.Commands;
using Newtonsoft.Json;
using SignalR.Client;

namespace Kudu.Client.Commands
{
    public class RemoteCommandExecutor : KuduRemoteClientBase, ICommandExecutor, IEventProvider
    {
        private Connection _connection;

        public RemoteCommandExecutor(string serviceUrl)
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

        public event Action<CommandEvent> CommandEvent;

        public void ExecuteCommand(string command)
        {
            _client.Post("run", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("command", command)))
                   .EnsureSuccessful();
        }

        public void CancelCommand()
        {
            _client.Post("cancel", new StringContent(String.Empty)).EnsureSuccessful();
        }

        public void Start()
        {
            if (_connection == null)
            {
                _connection = new Connection(ServiceUrl + "status");
                _connection.Credentials = Credentials;
                _connection.Received += OnReceived;
                _connection.Error += OnError;
            }

            _connection.Start().Wait();
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
            if (CommandEvent != null)
            {
                var commandEvent = JsonConvert.DeserializeObject<CommandEvent>(data);
                CommandEvent(commandEvent);
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
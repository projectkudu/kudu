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
    public class RemoteCommandExecutor : ICommandExecutor, IKuduClientCredentials
    {
        private readonly HttpClient _client;
        private readonly Connection _connection;
        private ICredentials _credentials;

        public RemoteCommandExecutor(string serviceUrl)
        {
            serviceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            _client = HttpClientHelper.Create(serviceUrl);

            _connection = new Connection(serviceUrl + "status");
            _connection.Received += data =>
            {
                if (CommandEvent != null)
                {
                    var commandEvent = JsonConvert.DeserializeObject<CommandEvent>(data);
                    CommandEvent(commandEvent);
                }
            };

            _connection.Start();
        }

        public ICredentials Credentials
        {
            get
            {
                return this._credentials;
            }
            set
            {
                this._credentials = value;
                this._client.SetClientCredentials(this._credentials);
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
    }
}
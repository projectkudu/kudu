using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.Commands;
using Newtonsoft.Json;
using SignalR.Client;

namespace Kudu.Client.Commands
{
    public class RemoteCommandExecutor : ICommandExecutor
    {
        private readonly HttpClient _client;
        private readonly Connection _connection;

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

        public event Action<CommandEvent> CommandEvent;

        public void ExecuteCommand(string command)
        {
            _client.Post("run", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, object>("command", command)))
                   .EnsureSuccessful();
        }

        public void CancelCommand()
        {
            _client.Post("cancel", new StringContent(String.Empty)).EnsureSuccessful();
        }
    }
}
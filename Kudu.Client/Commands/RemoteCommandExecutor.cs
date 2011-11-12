using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.Commands;
using Newtonsoft.Json;
using SignalR.Client;

namespace Kudu.Client.Commands
{
    public class RemoteCommandExecutor : KuduRemoteClientBase, ICommandExecutor
    {
        private readonly Connection _connection;

        public RemoteCommandExecutor(string serviceUrl)
            : base(serviceUrl)
        {
            _connection = new Connection(ServiceUrl + "status");
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
            _client.Post("run", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("command", command)))
                   .EnsureSuccessful();
        }

        public void CancelCommand()
        {
            _client.Post("cancel", new StringContent(String.Empty)).EnsureSuccessful();
        }
    }
}
using System.ComponentModel;
using System.Json;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Commands;

namespace Kudu.Services.Commands
{
    [ServiceContract]
    public class CommandService
    {
        private readonly ICommandExecutor _executor;
        public CommandService(ICommandExecutor executor)
        {
            _executor = executor;
        }

        [Description("Remotely executes the specified command.")]
        [WebInvoke(UriTemplate = "run")]
        public void Run(JsonObject input)
        {
            _executor.ExecuteCommand((string)input["command"]);
        }

        [Description("Cancels a pending command.")]
        [WebInvoke(UriTemplate = "cancel")]
        public void Cancel()
        {
            _executor.CancelCommand();
        }
    }
}

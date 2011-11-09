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

        [WebInvoke]
        public void Run(JsonObject input)
        {
            _executor.ExecuteCommand((string)input["command"]);
        }

        [WebInvoke]
        public void Cancel()
        {
            _executor.CancelCommand();
        }
    }
}

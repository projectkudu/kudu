using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Core.Commands;

namespace Kudu.Services.Commands {
    [ServiceContract]
    public class CommandController {
        private readonly ICommandExecutor _executor;
        public CommandController(ICommandExecutor executor) {
            _executor = executor;
        }

        [WebInvoke]
        public void Run(SimpleJson.JsonObject input) {
            _executor.ExecuteCommand((string)input["command"]);
        }

        [WebInvoke]
        public void Cancel() {
            _executor.CancelCommand();
        }
    }
}

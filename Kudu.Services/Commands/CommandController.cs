using System.Web.Mvc;
using Kudu.Core.Commands;

namespace Kudu.Services.Commands {
    public class CommandController : Controller {
        private readonly ICommandExecutor _executor;
        public CommandController(ICommandExecutor executor) {
            _executor = executor;
        }

        [HttpPost]
        public string Run(string command) {
            return _executor.ExecuteCommand(command);
        }
    }
}

using System.Web.Mvc;
using Kudu.Core.Commands;

namespace Kudu.Services.Commands {
    public class CommandController : Controller {
        private readonly ICommandExecutor _executor;
        private static object _exeLock = new object();

        public CommandController(ICommandExecutor executor) {
            _executor = executor;
        }

        [HttpPost]
        public void Run(string command) {
            _executor.ExecuteCommand(command);
        }
    }
}

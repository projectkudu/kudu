using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Core;
using Kudu.Core.Commands;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Commands
{
    public class CommandController : ApiController
    {
        private readonly ICommandExecutor _commandExecutor;

        public CommandController(ICommandExecutor commandExecutor)
        {
            _commandExecutor = commandExecutor;
        }

        /// <summary>
        /// Executes an arbitrary command line and return its output
        /// </summary>
        /// <param name="input">The command line to execute</param>
        /// <returns></returns>
        [HttpPost]
        public HttpResponseMessage ExecuteCommand(JObject input)
        {
            if (input == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string command = input.Value<string>("command");
            string workingDirectory = input.Value<string>("dir");
            CommandResult result = _commandExecutor.ExecuteCommand(command, workingDirectory);
            return Request.CreateResponse(HttpStatusCode.OK, result);
        }
    }
}

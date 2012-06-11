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

        [HttpPost]
        public HttpResponseMessage ExecuteCommand(JObject input)
        {
            if (input == null)
            {
                return Request.CreateResponse(HttpStatusCode.BadRequest);
            }

            string command = input.Value<string>("command");
            CommandResult result = _commandExecutor.ExecuteCommand(command);
            return Request.CreateResponse(HttpStatusCode.OK, result);
        }
    }
}

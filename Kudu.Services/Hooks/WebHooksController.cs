using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core.Hooks;

namespace Kudu.Services.Hooks
{
    public class WebHooksController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IWebHooksManager _hooksManager;

        public WebHooksController(ITracer tracer, WebHooksManager hooksManager)
        {
            _tracer = tracer;
            _hooksManager = hooksManager;
        }

        [HttpPost]
        public HttpResponseMessage Subscribe(WebHook webHook)
        {
            _hooksManager.AddWebHook(webHook);
            return Request.CreateResponse(HttpStatusCode.Created);
        }

        [HttpDelete]
        public HttpResponseMessage Unsubscribe(string hookAddress)
        {
            _hooksManager.RemoveWebHook(hookAddress);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpGet]
        public HttpResponseMessage GetWebHooks()
        {
            return Request.CreateResponse(HttpStatusCode.OK, _hooksManager.WebHooks);
        }
    }
}
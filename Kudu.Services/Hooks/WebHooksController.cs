using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core;
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
            try
            {
                WebHook webHookAdded = _hooksManager.AddWebHook(webHook);
                return Request.CreateResponse(HttpStatusCode.Created, webHookAdded);
            }
            catch (ConflictException)
            {
                _tracer.Trace("Web hook with address {0} already exists".FormatCurrentCulture(webHook.HookAddress));
                return Request.CreateResponse(HttpStatusCode.Conflict);
            }
        }

        [HttpDelete]
        public HttpResponseMessage Unsubscribe(string id)
        {
            _hooksManager.RemoveWebHook(id);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        [HttpGet]
        public HttpResponseMessage GetWebHooks()
        {
            return Request.CreateResponse(HttpStatusCode.OK, _hooksManager.WebHooks);
        }

        [HttpGet]
        public HttpResponseMessage GetWebHook(string id)
        {
            WebHook webHook = _hooksManager.GetWebHook(id);

            if (webHook == null)
            {
                Request.CreateResponse(HttpStatusCode.NotFound);
            }

            return Request.CreateResponse(HttpStatusCode.OK, webHook);
        }
    }
}
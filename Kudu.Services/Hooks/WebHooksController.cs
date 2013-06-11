using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
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
            catch (LockOperationException)
            {
                _tracer.Trace("Failed to acquire lock while subscribing {0}".FormatCurrentCulture(webHook.HookAddress));
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> PublishEvent(string hookEventType, object eventContent)
        {
            try
            {
                await _hooksManager.PublishEventAsync(hookEventType, eventContent);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (LockOperationException)
            {
                _tracer.Trace("Failed to acquire lock");
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [HttpDelete]
        public HttpResponseMessage Unsubscribe(string id)
        {
            try
            {
                _hooksManager.RemoveWebHook(id);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (LockOperationException)
            {
                _tracer.Trace("Failed to acquire lock while unsubscribing {0}".FormatCurrentCulture(id));
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetWebHooks()
        {
            try
            {
                return Request.CreateResponse(HttpStatusCode.OK, _hooksManager.WebHooks);
            }
            catch (LockOperationException)
            {
                _tracer.Trace("Failed to acquire lock");
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [HttpGet]
        public HttpResponseMessage GetWebHook(string id)
        {
            try
            {
                WebHook webHook = _hooksManager.GetWebHook(id);

                if (webHook == null)
                {
                    Request.CreateResponse(HttpStatusCode.NotFound);
                }

                return Request.CreateResponse(HttpStatusCode.OK, webHook);
            }
            catch (LockOperationException)
            {
                _tracer.Trace("Failed to acquire lock while getting {0}".FormatCurrentCulture(id));
                return Request.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
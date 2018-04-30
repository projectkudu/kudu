using System;
using System.Collections.Generic;
using System.Web;
using System.Web.Http.Filters;

namespace Kudu.Services
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class EnsureRequestIdHandlerAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            // https://github.com/projectkudu/kudu/issues/2547
            // On Mono, some async method calls may cause loss of context.
            // (A confirmed repro is the call to searchResource.Search in FeedExtensions.Search).
            // For now, just skip setting the response header if that's the case.
            var currentContext = HttpContext.Current;
            if (currentContext == null)
            {
                return;
            }

            IEnumerable<string> requestIds;
            if (actionExecutedContext.Request.Headers.TryGetValues(Constants.ArrLogIdHeader, out requestIds) ||
                actionExecutedContext.Request.Headers.TryGetValues(Constants.RequestIdHeader, out requestIds))
            {
                foreach (var rid in requestIds)
                {
                    // do not use Response from actionExecutedContext, if exception is throw, response will be null
                    currentContext.Response.AddHeader(Constants.RequestIdHeader, rid);
                }
            }
            else
            {
                string newRequestId = Guid.NewGuid().ToString();
                currentContext.Response.AddHeader(Constants.RequestIdHeader, newRequestId);
            }
        }
    }
}

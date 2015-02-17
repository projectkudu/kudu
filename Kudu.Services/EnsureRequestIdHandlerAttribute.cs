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
            IEnumerable<string> requestIds;
            if (actionExecutedContext.Request.Headers.TryGetValues(Constants.RequestIdHeader, out requestIds))
            {
                foreach (var rid in requestIds)
                {
                    // do not use Response from actionExecutedContext, if exception is throw, response will be null
                    HttpContext.Current.Response.AddHeader(Constants.RequestIdHeader, rid);
                }
            }
            else
            {
                string newRequestId = Guid.NewGuid().ToString();
                HttpContext.Current.Response.AddHeader(Constants.RequestIdHeader, newRequestId);
            }
        }
    }
}

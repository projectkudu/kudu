using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;

namespace Kudu.Services.Filters
{
    public sealed class RequestThrottleActionFilterAttribute : ActionFilterAttribute
    {
        int _concurrency = 0;

        public int MaxConcurrency { get; set; } = 5;

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            // If we've already reached the concurrency limit, return 429 (too many requests)
            // and ask to retry after 5 seconds
            if (_concurrency >= MaxConcurrency)
            {
                actionContext.Response = new HttpResponseMessage((HttpStatusCode)429);
                actionContext.Response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));
                return;
            }

            Interlocked.Increment(ref _concurrency);
        }

        public override void OnActionExecuted(HttpActionExecutedContext actionExecutedContext)
        {
            Interlocked.Decrement(ref _concurrency);
        }
    }
}

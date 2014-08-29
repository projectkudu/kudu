using System;
using System.Web.Http.Controllers;
using System.Web.Http.Filters;
using Kudu.Core.Tracing;

namespace Kudu.Services.Web
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class TraceDeprecatedActionAttribute : ActionFilterAttribute
    {
        private readonly IAnalytics _analytics;
        private readonly ITraceFactory _traceFactory;

        internal TraceDeprecatedActionAttribute(IAnalytics analytics, ITraceFactory traceFactory)
        {
            _analytics = analytics;
            _traceFactory = traceFactory;
        }

        public override void OnActionExecuting(HttpActionContext actionContext)
        {
            try
            {
                if (actionContext != null)
                {

                    var requestContext = actionContext.RequestContext;
                    if (requestContext != null && requestContext.RouteData != null)
                    {
                        var route = requestContext.RouteData.Route;

                        if (route != null && route.DataTokens != null)
                        {
                            // check whether route is marked as deprecated
                            if (route.DataTokens.ContainsKey(Infrastructure.RouteCollectionExtensions.DeprecatedKey))
                            {
                                var request = actionContext.Request;

                                _analytics.DeprecatedApiUsed(
                                    route.RouteTemplate,
                                    ToString(request.Headers.UserAgent),
                                    ToString(request.Method),
                                    request.RequestUri.AbsolutePath);
                            }
                        }
                    }
                }

                base.OnActionExecuting(actionContext);
            }
            catch(Exception ex)
            {
                _traceFactory.GetTracer().TraceWarning("Trace deprecated error: {0}", ex.ToString());
            }
        }

        private static string ToString(object obj)
        {
            return obj != null ? obj.ToString() : null;
        }
    }
}

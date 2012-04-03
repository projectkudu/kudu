using System;
using System.Web;
using System.Web.Routing;
using Ninject;
using Ninject.Extensions.Wcf;

namespace Kudu.Services.Web.Infrastruture
{
    public class HttpHandlerRouteHandler : IRouteHandler
    {
        private readonly Type _handlerType;
        public HttpHandlerRouteHandler(Type handlerType)
        {
            _handlerType = handlerType;
        }

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return (IHttpHandler)KernelContainer.Kernel.Get(_handlerType);
        }
    }

    public static class HttpHandlerExtensions
    {
        public static RouteBase MapHandler<THandler>(this RouteCollection routes, string name, string url)
        {
            var route = new Route(url, new HttpHandlerRouteHandler(typeof(THandler)));
            routes.Add(name, route);
            return route;
        }
    }
}
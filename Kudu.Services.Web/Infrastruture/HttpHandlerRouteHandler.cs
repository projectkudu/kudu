using System;
using System.Web;
using System.Web.Routing;
using Ninject;

namespace Kudu.Services.Web.Infrastruture
{
    public class HttpHandlerRouteHandler : IRouteHandler
    {
        private readonly Type _handlerType;
        private readonly IKernel _kernel;

        public HttpHandlerRouteHandler(IKernel kernel, Type handlerType)
        {
            _kernel = kernel;
            _handlerType = handlerType;
        }

        public IHttpHandler GetHttpHandler(RequestContext requestContext)
        {
            return (IHttpHandler)_kernel.Get(_handlerType);
        }
    }

    public static class HttpHandlerExtensions
    {
        public static RouteBase MapHandler<THandler>(this RouteCollection routes, IKernel kernel, string name, string url)
        {
            var route = new Route(url, new HttpHandlerRouteHandler(kernel, typeof(THandler)));
            routes.Add(name, route);
            return route;
        }
    }
}
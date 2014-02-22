using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Ninject;

namespace Kudu.Services.Web.Infrastructure
{
    public static class RouteCollectionExtensions
    {
        public static void MapHttpRouteDual(this RouteCollection routes, string name, string routeTemplate, object defaults, object constraints = null)
        {
            routes.MapHttpRoute(name + "-old", routeTemplate, defaults, constraints);
            routes.MapHttpRoute(name, "api/" + routeTemplate, defaults, constraints);
        }

        public static void MapHandlerDual<THandler>(this RouteCollection routes, IKernel kernel, string name, string url)
        {
            routes.MapHandler<THandler>(kernel, name + "-old", url);
            routes.MapHandler<THandler>(kernel, name, "api/" + url);
        }

        public static void MapHandler<THandler>(this RouteCollection routes, IKernel kernel, string name, string url)
        {
            var route = new Route(url, new HttpHandlerRouteHandler(kernel, typeof(THandler)));
            routes.Add(name, route);
        }
    }
}

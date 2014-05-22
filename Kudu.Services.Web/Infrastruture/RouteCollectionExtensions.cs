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
        public static void MapHttpWebJobsRoute(this RouteCollection routes, string name, string jobType, string routeTemplate, object defaults, object constraints = null)
        {
            // e.g. api/triggeredjobs/foo/history/17
            routes.MapHttpRoute(name, String.Format("api/{0}jobs{1}", jobType, routeTemplate), defaults, constraints);

            // e.g. jobs/triggered/foo/history/17 and api/jobs/triggered/foo/history/17
            routes.MapHttpRouteDual(name + "-old", String.Format("jobs/{0}{1}", jobType, routeTemplate), defaults, constraints);
        }

        public static void MapHttpRouteDual(this RouteCollection routes, string name, string routeTemplate, object defaults, object constraints = null)
        {
            routes.MapHttpRoute(name + "-noapi", routeTemplate, defaults, constraints);
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

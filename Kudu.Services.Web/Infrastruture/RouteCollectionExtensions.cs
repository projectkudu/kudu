using System;
using System.Linq;
using System.Web.Http;
using System.Web.Routing;
using Newtonsoft.Json;
using Ninject;

namespace Kudu.Services.Web.Infrastructure
{
    public static class RouteCollectionExtensions
    {
        public const string DeprecatedKey = "deprecated";

        public static void MapHttpWebJobsRoute(this RouteCollection routes, string name, string jobType, string routeTemplate, object defaults, object constraints = null)
        {
            // e.g. api/continuouswebjobs/foo
            routes.MapHttpRoute(name, String.Format("api/{0}webjobs{1}", jobType, routeTemplate), defaults, constraints, deprecated: false);

            // e.g. api/triggeredjobs/foo/history/17
            routes.MapHttpRoute(name + "-dep", String.Format("api/{0}jobs{1}", jobType, routeTemplate), defaults, constraints, deprecated: true);

            // e.g. jobs/triggered/foo/history/17 and api/jobs/triggered/foo/history/17
            routes.MapHttpRouteDual(name + "-old", String.Format("jobs/{0}{1}", jobType, routeTemplate), defaults, constraints, bothDeprecated: true);
        }

        public static void MapHttpProcessesRoute(this RouteCollection routes, string name, string routeTemplate, object defaults, object constraints = null)
        {
            // e.g. api/processes/3958/dump
            routes.MapHttpRoute(name + "-direct", "api/processes" + routeTemplate, defaults, constraints, deprecated: false);

            // e.g. api/diagnostics/processes/4845
            routes.MapHttpRouteDual(name, "diagnostics/processes" + routeTemplate, defaults, constraints);
        }

        public static void MapHttpRouteDual(this RouteCollection routes, string name, string routeTemplate, object defaults, object constraints = null, bool bothDeprecated = false)
        {
            routes.MapHttpRoute(name + "-dep", routeTemplate, defaults, constraints, deprecated: true);
            routes.MapHttpRoute(name, "api/" + routeTemplate, defaults, constraints, deprecated: bothDeprecated);
        }

        private static void MapHttpRoute(this RouteCollection routes, string name, string routeTemplate, object defaults, object constraints, bool deprecated)
        {
            if (deprecated)
            {
                name += "-dep";
            }

            var route = routes.MapHttpRoute(name, routeTemplate, defaults, constraints);

            if (deprecated)
            {
                DeprecateRoute(route);
            }
        }

        public static void MapHandlerDual<THandler>(this RouteCollection routes, IKernel kernel, string name, string url)
        {
            routes.MapHandler<THandler>(kernel, name + "-old", url, deprecated: true);
            routes.MapHandler<THandler>(kernel, name, "api/" + url, deprecated: false);
        }

        public static void MapHandler<THandler>(this RouteCollection routes, IKernel kernel, string name, string url, bool deprecated)
        {
            var route = new Route(url, new HttpHandlerRouteHandler(kernel, typeof(THandler)));
            routes.Add(name, route);
            if (deprecated)
            {
                DeprecateRoute(route);
            }
        }

        private static void DeprecateRoute(Route route)
        {
                route.DataTokens = new RouteValueDictionary();
                route.DataTokens[DeprecatedKey] = true;
        }
    }
}

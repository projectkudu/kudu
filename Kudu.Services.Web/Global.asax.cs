using System.Web.Mvc;
using System.Web.Routing;
using Kudu.Services.Deployment;
using RouteMagic;

namespace Kudu.Services {
    public class MvcApplication : System.Web.HttpApplication {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters) {
            filters.Add(new FormattedExceptionFilterAttribute());
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes) {
            var configuration = DependencyResolver.Current.GetService<IServerConfiguration>();

            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "proxy", // Route name
                configuration.HgServerRoot + "/{*url}", // URL with parameters
                new { controller = "Proxy", action = "ProxyRequest" } // Parameter defaults
            );

            routes.MapRoute(
                "InfoRefs", // Route name
                configuration.GitServerRoot + "/info/refs", // URL with parameters
                new { Controller = "InfoRefs", Action = "Execute" } // Parameter defaults
            );

            routes.MapRoute(
                "UploadPack", // Route name
                configuration.GitServerRoot + "/git-upload-pack", // URL with parameters
                new { Controller = "Rpc", Action = "UploadPack" } // Parameter defaults
            );

            routes.MapRoute(
                "ReceivePack", // Route name
                configuration.GitServerRoot + "/git-receive-pack", // URL with parameters
                new { Controller = "Rpc", Action = "ReceivePack" } // Parameter defaults
            );

            routes.MapHttpHandler(
                "DeploymentStatus",
                "deploy/status/{*url}",
               context => new DeploymentStatusHandler()
            );

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );
        }

        protected void Application_Start() {
            AreaRegistration.RegisterAllAreas();

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);
        }
    }
}
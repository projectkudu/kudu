using System.Web.Mvc;
using System.Web.Routing;

namespace Kudu.Services {
    public class MvcApplication : System.Web.HttpApplication {
        public static void RegisterGlobalFilters(GlobalFilterCollection filters) {
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes) {
            const string gitServerPathRoot = "SampleRepo.git";

            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "InfoRefs", // Route name
                gitServerPathRoot + "/info/refs", // URL with parameters
                new { Controller = "InfoRefs", Action = "Execute" } // Parameter defaults
            );

            routes.MapRoute(
                "UploadPack", // Route name
                gitServerPathRoot + "/git-upload-pack", // URL with parameters
                new { Controller = "Rpc", Action = "UploadPack" } // Parameter defaults
            );

            routes.MapRoute(
                "ReceivePack", // Route name
                gitServerPathRoot + "/git-receive-pack", // URL with parameters
                new { Controller = "Rpc", Action = "ReceivePack" } // Parameter defaults
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
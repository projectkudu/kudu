using System;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Web.Routing;
using Microsoft.ApplicationServer.Http.Activation;
using Microsoft.ApplicationServer.Http.Dispatcher;
using Ninject.Extensions.Wcf;
using SignalR.Routing;

namespace Kudu.Services {
    public class MvcApplication : System.Web.HttpApplication {

        public static void RegisterRoutes(RouteCollection routes) {
            var configuration = (IServerConfiguration)KernelContainer.Kernel.GetService(typeof(IServerConfiguration));
            var factory = GetFactory();

            routes.MapConnection<DeploymentStatusHandler>("DeploymentStatus", "deploy/status/{*operation}");
            routes.MapConnection<LiveCommandStatusHandler>("LiveCommandStatus", "live/command/status/{*operation}");
            routes.MapConnection<DevCommandStatusHandler>("DevCommandStatus", "dev/command/status/{*operation}");

            routes.Add(new ServiceRoute(configuration.HgServerRoot, factory, typeof(HgServer.ProxyController)));
            routes.Add(new ServiceRoute(configuration.GitServerRoot + "/info/refs", factory, typeof(GitServer.InfoRefsController)));
            routes.Add(new ServiceRoute(configuration.GitServerRoot, factory, typeof(GitServer.RpcController)));
            routes.Add(new ServiceRoute("deploy", factory, typeof(Deployment.DeployController)));
            routes.Add(new ServiceRoute("files", factory, typeof(Documents.FilesController)));
            routes.Add(new ServiceRoute("command", factory, typeof(Commands.CommandController)));
            routes.Add(new ServiceRoute("scm", factory, typeof(SourceControl.ScmController)));
            routes.Add(new ServiceRoute("appsettings", factory, typeof(Settings.AppSettingsController)));
            routes.Add(new ServiceRoute("connectionstrings", factory, typeof(Settings.ConnectionStringsController)));

            /*routes.MapRoute(
                "LiveCommandLine",
                "live/command/{action}/{id}",
                new { controller = "Command", action = "Index", id = UrlParameter.Optional, live = "live" }
            );

            routes.MapRoute(
                "DevCommandLine",
                "dev/command/{action}/{id}",
                new { controller = "Command", action = "Index", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                "LiveFiles",
                "live/files/{action}/{id}",
                new { controller = "Files", action = "Index", id = UrlParameter.Optional, live = "live" }
            );

            routes.MapRoute(
                "DevFiles",
                "dev/files/{action}/{id}",
                new { controller = "Files", action = "Index", id = UrlParameter.Optional }
            );

            routes.MapRoute(
                "Error",
                "error",
                new { controller = "Error", action = "Index" });

            routes.MapRoute(
                "CreateScm",
                "scm/create",
                new { controller = "DeploymentScm", action = "create" });

            routes.MapRoute(
                "DeleteScm",
                "scm/delete",
                new { controller = "DeploymentScm", action = "delete" });

            routes.MapRoute(
                "ScmKind",
                "scm/kind",
                new { controller = "DeploymentScm", action = "kind" });
            */
        }

        protected void Application_Start() {
            RegisterRoutes(RouteTable.Routes);
        }

        private static HttpServiceHostFactory GetFactory() {
            var factory = new HttpServiceHostFactory();

            // REVIEW: Set to max to accomodate large file uploads in initial scm setup.
            factory.Configuration.MaxBufferSize = Int32.MaxValue;
            factory.Configuration.MaxReceivedMessageSize = Int32.MaxValue;

            // Ensure that only our formatters are used
            factory.Configuration.Formatters.Clear();
            factory.Configuration.Formatters.Add(new Kudu.Core.Infrastructure.SimpleJsonMediaTypeFormatter());

            // Set IoC methods
            factory.Configuration.CreateInstance = CreateInstance;
            factory.Configuration.ReleaseInstance = ReleaseInstance;

            // Add the authorization handler on specific services method.
            var existingRequestHandlerFactory = factory.Configuration.RequestHandlers;
            factory.Configuration.RequestHandlers = (c, e, od) => {
                if (existingRequestHandlerFactory != null)
                    existingRequestHandlerFactory(c, e, od);

                if (e.Contract.ContractType == typeof(GitServer.InfoRefsController) || e.Contract.ContractType == typeof(GitServer.RpcController)) {
                    c.Insert(0, (HttpOperationHandler)KernelContainer.Kernel.GetService(typeof(Authorization.BasicAuthorizeHandler)));
                }
            };

            return factory;
        }

        private static object CreateInstance(Type type, InstanceContext context, HttpRequestMessage request) {
            return KernelContainer.Kernel.GetService(type);
        }

        private static void ReleaseInstance(InstanceContext context, object o) {
            KernelContainer.Kernel.Release(o);
        }
    }
}
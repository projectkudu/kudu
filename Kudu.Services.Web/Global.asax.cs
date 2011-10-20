using System;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Web.Routing;
using Kudu.Services.Deployment;
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

            // Source control servers
            MapServiceRoute<HgServer.ProxyController>(configuration.HgServerRoot, factory);
            MapServiceRoute<GitServer.InfoRefsController>(configuration.GitServerRoot + "/info/refs", factory);
            MapServiceRoute<GitServer.RpcController>(configuration.GitServerRoot, factory);


            // Source control interaction
            MapServiceRoute<SourceControl.ScmController>("scm", factory);
            MapServiceRoute<SourceControl.DeploymentScmController>("live/scm", factory);
            MapServiceRoute<SourceControl.DevelopmentScmService>("dev/scm", factory);

            // Deployment
            MapServiceRoute<Deployment.DeployController>("deploy", factory);

            // Files            
            MapServiceRoute<Documents.FilesController>("dev/files", factory);
            var liveFilesRoute = MapServiceRoute<Documents.FilesController>("live/files", factory);
            liveFilesRoute.Defaults = new RouteValueDictionary(new { live = true });

            // Commands
            MapServiceRoute<Commands.CommandController>("dev/command", factory);
            var liveCommandRoute = MapServiceRoute<Commands.CommandController>("live/command", factory);
            liveCommandRoute.Defaults = new RouteValueDictionary(new { live = true });

            // Settings
            MapServiceRoute<Settings.AppSettingsController>("appsettings", factory);
            MapServiceRoute<Settings.ConnectionStringsController>("connectionstrings", factory);
        }

        private static ServiceRoute MapServiceRoute<T>(string url, HttpServiceHostFactory factory) {
            var route = new ServiceRoute(url, factory, typeof(T));
            RouteTable.Routes.Add(route);
            return route;
        }

        protected void Application_Start() {
            RegisterRoutes(RouteTable.Routes);
        }

        private static HttpServiceHostFactory GetFactory() {
            var factory = new HttpServiceHostFactory();

            // REVIEW: Set to max to accomodate large file uploads in initial scm setup.
            factory.Configuration.MaxBufferSize = Int32.MaxValue;
            factory.Configuration.MaxReceivedMessageSize = Int32.MaxValue;
            factory.Configuration.EnableHelpPage = true;

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
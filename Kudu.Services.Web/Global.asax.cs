using System;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Web.Routing;
using Kudu.Core.Infrastructure;
using Kudu.Services.Authorization;
using Kudu.Services.Commands;
using Kudu.Services.Deployment;
using Kudu.Services.Documents;
using Kudu.Services.GitServer;
using Kudu.Services.HgServer;
using Kudu.Services.Settings;
using Kudu.Services.SourceControl;
using Microsoft.ApplicationServer.Http.Activation;
using Ninject;
using Ninject.Extensions.Wcf;
using SignalR;
using SignalR.Routing;

namespace Kudu.Services
{
    public class MvcApplication : System.Web.HttpApplication
    {

        public static void RegisterRoutes(RouteCollection routes)
        {
            var configuration = KernelContainer.Kernel.Get<IServerConfiguration>();
            var factory = GetFactory();

            routes.MapConnection<DeploymentStatusHandler>("DeploymentStatus", "deploy/status/{*operation}");
            routes.MapConnection<LiveCommandStatusHandler>("LiveCommandStatus", "live/command/status/{*operation}");
            routes.MapConnection<DevCommandStatusHandler>("DevCommandStatus", "dev/command/status/{*operation}");

            // Source control servers
            MapServiceRoute<ProxyService>(configuration.HgServerRoot, factory);
            MapServiceRoute<InfoRefsService>(configuration.GitServerRoot + "/info/refs", factory);
            MapServiceRoute<RpcService>(configuration.GitServerRoot, factory);


            // Source control interaction
            MapServiceRoute<SourceControlService>("scm", factory);
            MapServiceRoute<DeploymentSourceControlService>("live/scm", factory);
            MapServiceRoute<CloneService>("dev/scm", factory);

            // Deployment
            MapServiceRoute<DeploymentService>("deploy", factory);

            // Files            
            MapServiceRoute<FilesService>("dev/files", factory);
            var liveFilesRoute = MapServiceRoute<FilesService>("live/files", factory);
            liveFilesRoute.Defaults = new RouteValueDictionary(new { live = true });

            // Commands
            MapServiceRoute<CommandService>("dev/command", factory);
            var liveCommandRoute = MapServiceRoute<CommandService>("live/command", factory);
            liveCommandRoute.Defaults = new RouteValueDictionary(new { live = true });

            // Settings
            MapServiceRoute<AppSettingsService>("appsettings", factory);
            MapServiceRoute<ConnectionStringsService>("connectionstrings", factory);
        }

        private static ServiceRoute MapServiceRoute<T>(string url, HttpServiceHostFactory factory)
        {
            var route = new ServiceRoute(url, factory, typeof(T));
            RouteTable.Routes.Add(route);
            return route;
        }

        protected void Application_Start()
        {
            Signaler.Instance.DefaultTimeout = TimeSpan.FromSeconds(15);

            RegisterRoutes(RouteTable.Routes);
        }

        private static HttpServiceHostFactory GetFactory()
        {
            var factory = new HttpServiceHostFactory();

            // REVIEW: Set to max to accomodate large file uploads in initial scm setup.
            factory.Configuration.MaxBufferSize = Int32.MaxValue;
            factory.Configuration.MaxReceivedMessageSize = Int32.MaxValue;
            factory.Configuration.EnableHelpPage = true;

            // Ensure that only our formatters are used
            factory.Configuration.Formatters.Clear();
            factory.Configuration.Formatters.Add(new SimpleJsonMediaTypeFormatter());

            // Set IoC methods
            factory.Configuration.CreateInstance = CreateInstance;
            factory.Configuration.ReleaseInstance = ReleaseInstance;

            // Add the authorization handler on specific services method.
            var existingRequestHandlerFactory = factory.Configuration.RequestHandlers;
            factory.Configuration.RequestHandlers = (c, e, od) =>
            {
                if (existingRequestHandlerFactory != null)
                {
                    existingRequestHandlerFactory(c, e, od);
                }

                c.Insert(0, KernelContainer.Kernel.Get<BasicAuthorizeHandler>());
            };

            return factory;
        }

        private static object CreateInstance(Type type, InstanceContext context, HttpRequestMessage request)
        {
            return KernelContainer.Kernel.GetService(type);
        }

        private static void ReleaseInstance(InstanceContext context, object o)
        {
            KernelContainer.Kernel.Release(o);
        }
    }
}
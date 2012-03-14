using System;
using System.Net.Http;
using System.Net.Http.Formatting;
using System.ServiceModel;
using System.ServiceModel.Activation;
using System.Web.Routing;
using Kudu.Services.Authorization;
using Kudu.Services.Deployment;
using Kudu.Services.Documents;
using Kudu.Services.GitServer;
using Kudu.Services.Performance;
using Kudu.Services.Settings;
using Kudu.Services.SourceControl;
using Kudu.Services.Web;
using Kudu.Services.Web.Tracing;
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

            routes.MapConnection<DeploymentStatusHandler>("DeploymentStatus", "deployments/status/{*operation}");
            // routes.MapConnection<LiveCommandStatusHandler>("LiveCommandStatus", "live/command/status/{*operation}");
            // routes.MapConnection<DevCommandStatusHandler>("DevCommandStatus", "dev/command/status/{*operation}");

            // Source control servers
            // Mercurial
            // MapServiceRoute<ProxyService>(configuration.HgServerRoot, factory);

            // Git
            MapServiceRoute<InfoRefsService>(configuration.GitServerRoot + "/info/refs", factory);
            MapServiceRoute<RpcService>(configuration.GitServerRoot, factory);


            // Source control interaction
            MapServiceRoute<DeploymentSourceControlService>("live/scm", factory);

            // Deployment
            MapServiceRoute<DeploymentService>("deployments", factory);

            // Files            
            // MapServiceRoute<FilesService>("dev/files", factory);
            var liveFilesRoute = MapServiceRoute<FilesService>("live/files", factory);
            liveFilesRoute.Defaults = new RouteValueDictionary(new { live = true });

            // Commands
            // MapServiceRoute<CommandService>("dev/command", factory);
            // var liveCommandRoute = MapServiceRoute<CommandService>("live/command", factory);
            // liveCommandRoute.Defaults = new RouteValueDictionary(new { live = true });

            if (AppSettings.SettingsEnabled)
            {
                // Settings
                MapServiceRoute<AppSettingsService>("appsettings", factory);
                MapServiceRoute<ConnectionStringsService>("connectionstrings", factory);
            }

            MapServiceRoute<DiagnosticsService>("dump", factory);
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
            factory.Configuration.TransferMode = TransferMode.Streamed;

            factory.Configuration.Formatters.Clear();
            factory.Configuration.Formatters.Add(new JsonValueMediaTypeFormatter());
            factory.Configuration.Formatters.Add(new JsonMediaTypeFormatter());

#if DEBUG
            factory.Configuration.IncludeExceptionDetail = true;
#endif
            // Set IoC methods
            factory.Configuration.CreateInstance = CreateInstance;
            factory.Configuration.ReleaseInstance = ReleaseInstance;

            factory.Configuration.ErrorHandlers = (handlers, service, op) =>
            {
                handlers.Add(KernelContainer.Kernel.Get<TraceErrorHandler>());
            };

            // Add the authorization handler on specific services method.
            var existingRequestHandlerFactory = factory.Configuration.RequestHandlers;
            factory.Configuration.RequestHandlers = (c, e, od) =>
            {
                if (existingRequestHandlerFactory != null)
                {
                    existingRequestHandlerFactory(c, e, od);
                }

                // Enable authentication based on the configuration setting
                if (AppSettings.AuthenticationEnabled)
                {
                    c.Insert(0, KernelContainer.Kernel.Get<BasicAuthorizeHandler>());
                }
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
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Http;
using System.Web.Routing;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Editor;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SSHKey;
using Kudu.Core.Tracing;
using Kudu.Services.GitServer;
using Kudu.Services.Performance;
using Kudu.Services.SSHKey;
using Kudu.Services.Web.Infrastruture;
using Kudu.Services.Web.Services;
using Kudu.Services.Web.Tracing;
using Microsoft.Web.Infrastructure.DynamicModuleHelper;
using Ninject;
using Ninject.Activation;
using XmlSettings;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]

namespace Kudu.Services.Web.App_Start
{
    public static class NinjectServices
    {
        /// <summary>
        /// Root directory that contains the VS target files
        /// </summary>
        private const string SdkRootDirectory = "msbuild";

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            DynamicModuleUtility.RegisterModule(typeof(OnePerRequestModule));
            CreateKernel();
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop()
        {
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();

            RegisterServices(kernel);

            return kernel;
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        private static void RegisterServices(IKernel kernel)
        {
            var serverConfiguration = new ServerConfiguration();
            var gitConfiguration = new RepositoryConfiguration
            {
                Username = AppSettings.GitUsername,
                Email = AppSettings.GitEmail,
                TraceLevel = AppSettings.TraceLevel
            };

            IEnvironment environment = GetEnvironment();

            // General
            kernel.Bind<HttpContextBase>().ToMethod(context => new HttpContextWrapper(HttpContext.Current));
            kernel.Bind<IEnvironment>().ToConstant(environment);
            kernel.Bind<IServerConfiguration>().ToConstant(serverConfiguration);
            kernel.Bind<IFileSystem>().To<FileSystem>().InSingletonScope();
            kernel.Bind<RepositoryConfiguration>().ToConstant(gitConfiguration);

            string sdkPath = Path.Combine(HttpRuntime.AppDomainAppPath, SdkRootDirectory);
            kernel.Bind<IBuildPropertyProvider>().ToConstant(new BuildPropertyProvider());

            if (AppSettings.TraceEnabled)
            {
                string tracePath = Path.Combine(environment.RootPath, Constants.TracePath, Constants.TraceFile);
                System.Func<ITracer> createTracerThunk = () => new Tracer(tracePath);

                // First try to use the current request profiler if any, otherwise create a new one
                var traceFactory = new TracerFactory(() => TraceServices.CurrentRequestTracer ?? createTracerThunk());

                kernel.Bind<ITracer>().ToMethod(context => TraceServices.CurrentRequestTracer ?? NullTracer.Instance);
                kernel.Bind<ITraceFactory>().ToConstant(traceFactory);
                TraceServices.SetTraceFactory(createTracerThunk);
            }
            else
            {
                // Return No-op providers
                kernel.Bind<ITracer>().ToConstant(NullTracer.Instance).InSingletonScope();
                kernel.Bind<ITraceFactory>().ToConstant(NullTracerFactory.Instance).InSingletonScope();
            }


            // Setup the deployment lock
            string lockPath = Path.Combine(environment.SiteRootPath, Constants.LockPath);
            string deploymentLockPath = Path.Combine(lockPath, Constants.DeploymentLockFile);
            string sshKeyLockPath = Path.Combine(lockPath, Constants.SSHKeyLockFile);
            string initLockPath = Path.Combine(lockPath, Constants.InitLockFile);

            var deploymentLock = new LockFile(kernel.Get<ITraceFactory>(), deploymentLockPath);
            var initLock = new LockFile(kernel.Get<ITraceFactory>(), initLockPath);
            var sshKeyLock = new LockFile(kernel.Get<ITraceFactory>(), sshKeyLockPath);

            kernel.Bind<IOperationLock>().ToConstant(sshKeyLock).WhenInjectedInto<SSHKeyController>();
            kernel.Bind<IOperationLock>().ToConstant(deploymentLock);

            // Setup the diagnostics service to collect information from the following paths:
            // 1. The deployments folder
            // 2. The profile dump
            // 3. The npm log
            var paths = new[] { 
                environment.DeploymentCachePath,
                Path.Combine(environment.RootPath, Constants.LogFilesPath),
                Path.Combine(environment.WebRootPath, Constants.NpmDebugLogFile),
            };

            kernel.Bind<DiagnosticsController>().ToMethod(context => new DiagnosticsController(paths));

            // LogStream service
            kernel.Bind<LogStreamManager>().ToMethod(context => new LogStreamManager(Path.Combine(environment.RootPath, Constants.LogFilesPath),
                                                                                     context.Kernel.Get<ITracer>()));

            // Deployment Service
            kernel.Bind<ISettings>().ToMethod(context => new XmlSettings.Settings(GetSettingsPath(environment)));
            kernel.Bind<IDeploymentSettingsManager>().To<DeploymentSettingsManager>();

            kernel.Bind<ISiteBuilderFactory>().To<SiteBuilderFactory>()
                                             .InRequestScope();

            kernel.Bind<IServerRepository>().ToMethod(context => new GitExeServer(environment.RepositoryPath,
                                                                                  initLock,
                                                                                  context.Kernel.Get<IDeploymentEnvironment>(),
                                                                                  context.Kernel.Get<ITraceFactory>()))
                                            .InRequestScope();

            kernel.Bind<ILogger>().ToConstant(NullLogger.Instance);
            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InRequestScope();
            kernel.Bind<ISSHKeyManager>().To<SSHKeyManager>()
                                             .InRequestScope();

            kernel.Bind<IDeploymentRepository>().ToMethod(context => new GitDeploymentRepository(environment.RepositoryPath, context.Kernel.Get<ITraceFactory>()))
                                                .InRequestScope();

            // Git server
            kernel.Bind<IDeploymentEnvironment>().To<DeploymentEnvrionment>();

            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(environment.RepositoryPath,
                                                                           initLock,
                                                                           context.Kernel.Get<IDeploymentEnvironment>(),
                                                                           context.Kernel.Get<ITraceFactory>()))
                                     .InRequestScope();

            // Editor
            kernel.Bind<IProjectSystem>().ToMethod(context => GetEditorProjectSystem(environment, context))
                                         .InRequestScope();

            // Command executor
            kernel.Bind<ICommandExecutor>().ToMethod(context => GetCommandExecutor(environment, context))
                                           .InRequestScope();

            RegisterRoutes(kernel, RouteTable.Routes);
        }

        public static void RegisterRoutes(IKernel kernel, RouteCollection routes)
        {
            var configuration = kernel.Get<IServerConfiguration>();
            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.LocalOnly;
            var jsonFormatter = new JsonMediaTypeFormatter();
            GlobalConfiguration.Configuration.Formatters.Add(jsonFormatter);
            GlobalConfiguration.Configuration.DependencyResolver = new NinjectWebApiDependencyResolver(kernel);

            // the scenario is to have kudu service running but w/o git functionalities.
            // this is utilized by windows azures where we try to avoid deployment collision - if git disabled.
            // we intentionally on block git related operation - not other repository-related such as /deployment
            if (!AppSettings.DisableGit)
            {
                // Git Service
                routes.MapHttpRoute("git-info-refs", configuration.GitServerRoot + "/info/refs", new { controller = "InfoRefs", action = "Execute" });

                // Push url
                routes.MapHandler<ReceivePackHandler>(kernel, "git-receive-pack", configuration.GitServerRoot + "/git-receive-pack");

                // Fetch Hook
                routes.MapHandler<FetchHandler>(kernel, "fetch", "deploy");

                // Clone url
                routes.MapHandler<UploadPackHandler>(kernel, "git-upload-pack", configuration.GitServerRoot + "/git-upload-pack");
            }

            // Scm (deployment repository)
            routes.MapHttpRoute("scm-info", "scm/info", new { controller = "LiveScm", action = "GetRepositoryInfo" });
            routes.MapHttpRoute("scm-clean", "scm/clean", new { controller = "LiveScm", action = "Clean" });
            routes.MapHttpRoute("scm-delete", "scm", new { controller = "LiveScm", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // These older scm routes are there for backward compat, and should eventually be deleted once clients are changed.
            routes.MapHttpRoute("live-scm-info", "live/scm/info", new { controller = "LiveScm", action = "GetRepositoryInfo" });
            routes.MapHttpRoute("live-scm-clean", "live/scm/clean", new { controller = "LiveScm", action = "Clean" });
            routes.MapHttpRoute("live-scm-delete", "live/scm", new { controller = "LiveScm", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Live Files
            routes.MapHttpRoute("all-files", "files", new { controller = "Files", action = "GetFiles" });
            routes.MapHttpRoute("one-file", "files/{*path}", new { controller = "Files", action = "GetFile" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("save-file", "files/{*path}", new { controller = "Files", action = "Save" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("delete-file", "files/{*path}", new { controller = "Files", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // These older files routes are there for backward compat, and should eventually be deleted once clients are changed.
            routes.MapHttpRoute("old-all-files", "live/files", new { controller = "Files", action = "GetFiles" });
            routes.MapHttpRoute("old-one-file", "live/files/{*path}", new { controller = "Files", action = "GetFile" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("old-save-file", "live/files/{*path}", new { controller = "Files", action = "Save" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("old-delete-file", "live/files/{*path}", new { controller = "Files", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Live Command Line
            routes.MapHttpRoute("execute-command", "command", new { controller = "Command", action = "ExecuteCommand" }, new { verb = new HttpMethodConstraint("POST") });

            // Deployments
            routes.MapHttpRoute("all-deployments", "deployments", new { controller = "Deployment", action = "GetDeployResults" });
            routes.MapHttpRoute("one-deployment-get", "deployments/{id}", new { controller = "Deployment", action = "GetResult" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("one-deployment-put", "deployments/{id}", new { controller = "Deployment", action = "Deploy" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("one-deployment-delete", "deployments/{id}", new { controller = "Deployment", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRoute("one-deployment-log", "deployments/{id}/log", new { controller = "Deployment", action = "GetLogEntry" });
            routes.MapHttpRoute("one-deployment-log-details", "deployments/{id}/log/{logId}", new { controller = "Deployment", action = "GetLogEntryDetails" });

            // SSHKey
            routes.MapHttpRoute("get-sshkey", "sshkey", new { controller = "SSHKey", action = "GetPublicKey" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("put-sshkey", "sshkey", new { controller = "SSHKey", action = "SetPrivateKey" }, new { verb = new HttpMethodConstraint("PUT") });

            // Environment
            routes.MapHttpRoute("get-env", "environment", new { controller = "Environment", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });

            // Settings
            routes.MapHttpRoute("set-setting", "settings", new { controller = "Settings", action = "Set" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRoute("get-all-settings", "settings", new { controller = "Settings", action = "GetAll" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("get-setting", "settings/{key}", new { controller = "Settings", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("delete-setting", "settings/{key}", new { controller = "Settings", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Diagnostics
            routes.MapHttpRoute("diagnostics", "dump", new { controller = "Diagnostics", action = "GetLog" });

            // LogStream
            routes.MapHandler<LogStreamHandler>(kernel, "logstream", "logstream/{*path}");
        }

        private static IProjectSystem GetEditorProjectSystem(IEnvironment environment, IContext context)
        {
            return new ProjectSystem(environment.WebRootPath);
        }

        private static ICommandExecutor GetCommandExecutor(IEnvironment environment, IContext context)
        {
            if (System.String.IsNullOrEmpty(environment.RepositoryPath))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            return new CommandExecutor(environment.RootPath);
        }

        private static string GetSettingsPath(IEnvironment environment)
        {
            return Path.Combine(environment.DeploymentCachePath, Constants.DeploySettingsPath);
        }

        private static IEnvironment GetEnvironment()
        {
            string siteRoot = PathResolver.ResolveSiteRootPath();
            string root = Path.GetFullPath(Path.Combine(siteRoot, ".."));
            string webRootPath = Path.Combine(siteRoot, Constants.WebRoot);
            string deployCachePath = Path.Combine(siteRoot, Constants.DeploymentCachePath);
            string sshKeyPath = Path.Combine(siteRoot, Constants.SSHKeyPath);
            string repositoryPath = Path.Combine(siteRoot, Constants.RepositoryPath);
            string tempPath = Path.GetTempPath();
            string deploymentTempPath = Path.Combine(tempPath, Constants.RepositoryPath);
            string scriptPath = Path.Combine(HttpRuntime.BinDirectory, Constants.ScriptsPath);

            return new Environment(
                                   new FileSystem(),
                                   root,
                                   siteRoot,
                                   tempPath,
                                   repositoryPath,
                                   webRootPath,
                                   deployCachePath,
                                   sshKeyPath,
                                   AppSettings.NuGetCachePath,
                                   scriptPath);
        }

        private class DeploymentManagerFactory : IDeploymentManagerFactory
        {
            private readonly System.Func<IDeploymentManager> _factory;
            public DeploymentManagerFactory(System.Func<IDeploymentManager> factory)
            {
                _factory = factory;
            }

            public IDeploymentManager CreateDeploymentManager()
            {
                return _factory();
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Http.Formatting;
using System.Web;
using System.Web.Hosting;
using System.Web.Http;
using System.Web.Routing;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SiteExtensions;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Commands;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Jobs;
using Kudu.Core.Settings;
using Kudu.Core.SiteExtensions;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SSHKey;
using Kudu.Core.Tracing;
using Kudu.Services.Diagnostics;
using Kudu.Services.GitServer;
using Kudu.Services.Infrastructure;
using Kudu.Services.Performance;
using Kudu.Services.ServiceHookHandlers;
using Kudu.Services.SSHKey;
using Kudu.Services.Web.Infrastructure;
using Kudu.Services.Web.Services;
using Kudu.Services.Web.Tracing;
using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Ninject;
using Ninject.Activation;
using Ninject.Web.Common;
using Owin;
using XmlSettings;

[assembly: WebActivator.PreApplicationStartMethod(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Start")]
[assembly: WebActivator.ApplicationShutdownMethodAttribute(typeof(Kudu.Services.Web.App_Start.NinjectServices), "Stop")]
[assembly: OwinStartup(typeof(Kudu.Services.Web.App_Start.NinjectServices.SignalRStartup))]

namespace Kudu.Services.Web.App_Start
{
    public static class NinjectServices
    {
        /// <summary>
        /// Root directory that contains the VS target files
        /// </summary>
        private const string SdkRootDirectory = "msbuild";

        private static readonly Bootstrapper _bootstrapper = new Bootstrapper();

        // Due to a bug in Ninject we can't use Dispose to clean up LockFile so we shut it down manually
        private static DeploymentLockFile _deploymentLock;

        private static event Action Shutdown;

        /// <summary>
        /// Starts the application
        /// </summary>
        public static void Start()
        {
            HttpApplication.RegisterModule(typeof(OnePerRequestHttpModule));
            HttpApplication.RegisterModule(typeof(NinjectHttpModule));
            _bootstrapper.Initialize(CreateKernel);
        }

        /// <summary>
        /// Stops the application.
        /// </summary>
        public static void Stop()
        {
            if (Shutdown != null)
            {
                Shutdown();
            }

            if (_deploymentLock != null)
            {
                _deploymentLock.TerminateAsyncLocks();
                _deploymentLock = null;
            }

            _bootstrapper.ShutDown();
        }

        /// <summary>
        /// Creates the kernel that will manage your application.
        /// </summary>
        /// <returns>The created kernel.</returns>
        private static IKernel CreateKernel()
        {
            var kernel = new StandardKernel();
            kernel.Bind<Func<IKernel>>().ToMethod(ctx => () => new Bootstrapper().Kernel);
            kernel.Bind<IHttpModule>().To<HttpApplicationInitializationHttpModule>();
            kernel.Components.Add<INinjectHttpApplicationPlugin, NinjectHttpApplicationPlugin>();

            RegisterServices(kernel);
            return kernel;
        }

        /// <summary>
        /// Load your modules or register your services here!
        /// </summary>
        /// <param name="kernel">The kernel.</param>
        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity", Justification = "")]
        private static void RegisterServices(IKernel kernel)
        {
            var serverConfiguration = new ServerConfiguration();

            // Make sure %HOME% is correctly set
            EnsureHomeEnvironmentVariable();

            EnsureSiteBitnessEnvironmentVariable();

            IEnvironment environment = GetEnvironment();

            // Per request environment
            kernel.Bind<IEnvironment>().ToMethod(context => GetEnvironment(context.Kernel.Get<IDeploymentSettingsManager>()))
                                             .InRequestScope();

            // General
            kernel.Bind<HttpContextBase>().ToMethod(context => new HttpContextWrapper(HttpContext.Current))
                                             .InRequestScope();
            kernel.Bind<IServerConfiguration>().ToConstant(serverConfiguration);

            kernel.Bind<IBuildPropertyProvider>().ToConstant(new BuildPropertyProvider());

            System.Func<ITracer> createTracerThunk = () => GetTracer(environment, kernel);
            System.Func<ILogger> createLoggerThunk = () => GetLogger(environment, kernel);

            // First try to use the current request profiler if any, otherwise create a new one
            var traceFactory = new TracerFactory(() => TraceServices.CurrentRequestTracer ?? createTracerThunk());

            kernel.Bind<ITracer>().ToMethod(context => TraceServices.CurrentRequestTracer ?? NullTracer.Instance);
            kernel.Bind<ITraceFactory>().ToConstant(traceFactory);
            TraceServices.SetTraceFactory(createTracerThunk, createLoggerThunk);

            // Setup the deployment lock
            string lockPath = Path.Combine(environment.SiteRootPath, Constants.LockPath);
            string deploymentLockPath = Path.Combine(lockPath, Constants.DeploymentLockFile);
            string statusLockPath = Path.Combine(lockPath, Constants.StatusLockFile);
            string sshKeyLockPath = Path.Combine(lockPath, Constants.SSHKeyLockFile);
            string hooksLockPath = Path.Combine(lockPath, Constants.HooksLockFile);

            _deploymentLock = new DeploymentLockFile(deploymentLockPath, kernel.Get<ITraceFactory>());
            _deploymentLock.InitializeAsyncLocks();

            var statusLock = new LockFile(statusLockPath, kernel.Get<ITraceFactory>());
            var sshKeyLock = new LockFile(sshKeyLockPath, kernel.Get<ITraceFactory>());
            var hooksLock = new LockFile(hooksLockPath, kernel.Get<ITraceFactory>());

            kernel.Bind<IOperationLock>().ToConstant(sshKeyLock).WhenInjectedInto<SSHKeyController>();
            kernel.Bind<IOperationLock>().ToConstant(statusLock).WhenInjectedInto<DeploymentStatusManager>();
            kernel.Bind<IOperationLock>().ToConstant(hooksLock).WhenInjectedInto<WebHooksManager>();
            kernel.Bind<IOperationLock>().ToConstant(_deploymentLock);

            var shutdownDetector = new ShutdownDetector();
            shutdownDetector.Initialize();

            IDeploymentSettingsManager noContextDeploymentsSettingsManager =
                new DeploymentSettingsManager(new XmlSettings.Settings(GetSettingsPath(environment)));

            var noContextTraceFactory = new TracerFactory(() => GetTracerWithoutContext(environment, noContextDeploymentsSettingsManager));

            kernel.Bind<IAnalytics>().ToMethod(context => new Analytics(context.Kernel.Get<IDeploymentSettingsManager>(),
                                                                        context.Kernel.Get<IServerConfiguration>(),
                                                                        noContextTraceFactory));

            // Trace unhandled (crash) exceptions.
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                if (ex != null)
                {
                    kernel.Get<IAnalytics>().UnexpectedException(ex);
                }
            };

            // Trace shutdown event
            // Cannot use shutdownDetector.Token.Register because of race condition
            // with NinjectServices.Stop via WebActivator.ApplicationShutdownMethodAttribute
            Shutdown += () => TraceShutdown(environment, noContextDeploymentsSettingsManager);

            // LogStream service
            // The hooks and log stream start endpoint are low traffic end-points. Re-using it to avoid creating another lock
            var logStreamManagerLock = hooksLock;
            kernel.Bind<LogStreamManager>().ToMethod(context => new LogStreamManager(Path.Combine(environment.RootPath, Constants.LogFilesPath),
                                                                                     context.Kernel.Get<IEnvironment>(),
                                                                                     context.Kernel.Get<IDeploymentSettingsManager>(),
                                                                                     context.Kernel.Get<ITracer>(),
                                                                                     shutdownDetector,
                                                                                     logStreamManagerLock));

            kernel.Bind<InfoRefsController>().ToMethod(context => new InfoRefsController(t => context.Kernel.Get(t)))
                                             .InRequestScope();

            kernel.Bind<CustomGitRepositoryHandler>().ToMethod(context => new CustomGitRepositoryHandler(t => context.Kernel.Get(t)))
                                                     .InRequestScope();

            // Deployment Service
            kernel.Bind<ISettings>().ToMethod(context => new XmlSettings.Settings(GetSettingsPath(environment)))
                                             .InRequestScope();
            kernel.Bind<IDeploymentSettingsManager>().To<DeploymentSettingsManager>()
                                             .InRequestScope();

            kernel.Bind<IDeploymentStatusManager>().To<DeploymentStatusManager>()
                                             .InRequestScope();

            kernel.Bind<ISiteBuilderFactory>().To<SiteBuilderFactory>()
                                             .InRequestScope();

            kernel.Bind<IWebHooksManager>().To<WebHooksManager>()
                                             .InRequestScope();

            ITriggeredJobsManager triggeredJobsManager = new TriggeredJobsManager(
                noContextTraceFactory,
                kernel.Get<IEnvironment>(),
                kernel.Get<IDeploymentSettingsManager>(),
                kernel.Get<IAnalytics>(),
                kernel.Get<IWebHooksManager>());
            kernel.Bind<ITriggeredJobsManager>().ToConstant(triggeredJobsManager)
                                             .InTransientScope();

            IContinuousJobsManager continuousJobManager = new ContinuousJobsManager(
                noContextTraceFactory,
                kernel.Get<IEnvironment>(),
                kernel.Get<IDeploymentSettingsManager>(),
                kernel.Get<IAnalytics>());

            triggeredJobsManager.CleanupDeletedJobs();
            continuousJobManager.CleanupDeletedJobs();

            kernel.Bind<IContinuousJobsManager>().ToConstant(continuousJobManager)
                                             .InTransientScope();

            kernel.Bind<ILogger>().ToMethod(context => GetLogger(environment, context.Kernel))
                                             .InRequestScope();

            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InRequestScope();
            kernel.Bind<IAutoSwapHandler>().To<AutoSwapHandler>()
                                             .InRequestScope();
            kernel.Bind<ISSHKeyManager>().To<SSHKeyManager>()
                                             .InRequestScope();

            kernel.Bind<IRepositoryFactory>().ToMethod(context => _deploymentLock.RepositoryFactory = new RepositoryFactory(context.Kernel.Get<IEnvironment>(),
                                                                                                                            context.Kernel.Get<IDeploymentSettingsManager>(),
                                                                                                                            context.Kernel.Get<ITraceFactory>(),
                                                                                                                            context.Kernel.Get<HttpContextBase>()))
                                             .InRequestScope();

            kernel.Bind<IApplicationLogsReader>().To<ApplicationLogsReader>()
                                             .InSingletonScope();

            // Git server
            kernel.Bind<IDeploymentEnvironment>().To<DeploymentEnvrionment>();

            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(context.Kernel.Get<IEnvironment>(),
                                                                           _deploymentLock,
                                                                           GetRequestTraceFile(context.Kernel),
                                                                           context.Kernel.Get<IRepositoryFactory>(),
                                                                           context.Kernel.Get<IDeploymentEnvironment>(),
                                                                           context.Kernel.Get<IDeploymentSettingsManager>(),
                                                                           context.Kernel.Get<ITraceFactory>()))
                                     .InRequestScope();

            // Git Servicehook parsers
            kernel.Bind<IServiceHookHandler>().To<GenericHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<GitHubHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<BitbucketHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<DropboxHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<CodePlexHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<CodebaseHqHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<GitlabHqHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<GitHubCompatHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<KilnHgHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<VSOHandler>().InRequestScope();
            kernel.Bind<IServiceHookHandler>().To<OneDriveHandler>().InRequestScope();

            // SiteExtensions
            kernel.Bind<ISiteExtensionManager>().To<SiteExtensionManager>().InRequestScope();

            // Command executor
            kernel.Bind<ICommandExecutor>().ToMethod(context => GetCommandExecutor(environment, context))
                                           .InRequestScope();

            MigrateSite(environment, noContextDeploymentsSettingsManager);
            RemoveOldTracePath(environment);
            RemoveTempFileFromUserDrive(environment);

            // Temporary fix for https://github.com/npm/npm/issues/5905
            EnsureNpmGlobalDirectory();
            EnsureUserProfileDirectory();

            RegisterRoutes(kernel, RouteTable.Routes);

            // Register the default hubs route: ~/signalr
            GlobalHost.DependencyResolver = new SignalRNinjectDependencyResolver(kernel);
            GlobalConfiguration.Configuration.Filters.Add(
                new TraceDeprecatedActionAttribute(
                    kernel.Get<IAnalytics>(),
                    kernel.Get<ITraceFactory>()));
            GlobalConfiguration.Configuration.Filters.Add(new EnsureRequestIdHandlerAttribute());
        }

        public static class SignalRStartup
        {
            public static void Configuration(IAppBuilder app)
            {
                app.MapSignalR<PersistentCommandController>("/api/commandstream");
                app.MapSignalR("/api/filesystemhub", new HubConfiguration());
            }
        }

        public class SignalRNinjectDependencyResolver : DefaultDependencyResolver
        {
            private readonly IKernel _kernel;

            public SignalRNinjectDependencyResolver(IKernel kernel)
            {
                _kernel = kernel;
            }

            public override object GetService(Type serviceType)
            {
                return _kernel.TryGet(serviceType) ?? base.GetService(serviceType);
            }

            public override IEnumerable<object> GetServices(Type serviceType)
            {
                return System.Linq.Enumerable.Concat(_kernel.GetAll(serviceType), base.GetServices(serviceType));
            }
        }

        public static void RegisterRoutes(IKernel kernel, RouteCollection routes)
        {
            var configuration = kernel.Get<IServerConfiguration>();
            GlobalConfiguration.Configuration.Formatters.Clear();
            GlobalConfiguration.Configuration.IncludeErrorDetailPolicy = IncludeErrorDetailPolicy.Always;

            var jsonFormatter = new JsonMediaTypeFormatter();
            GlobalConfiguration.Configuration.Formatters.Add(jsonFormatter);
            GlobalConfiguration.Configuration.DependencyResolver = new NinjectWebApiDependencyResolver(kernel);
            GlobalConfiguration.Configuration.Filters.Add(new TraceExceptionFilterAttribute());

            // Git Service
            routes.MapHttpRoute("git-info-refs-root", "info/refs", new { controller = "InfoRefs", action = "Execute" });
            routes.MapHttpRoute("git-info-refs", configuration.GitServerRoot + "/info/refs", new { controller = "InfoRefs", action = "Execute" });

            // Push url
            routes.MapHandler<ReceivePackHandler>(kernel, "git-receive-pack-root", "git-receive-pack", deprecated: false);
            routes.MapHandler<ReceivePackHandler>(kernel, "git-receive-pack", configuration.GitServerRoot + "/git-receive-pack", deprecated: false);

            // Fetch Hook
            routes.MapHandler<FetchHandler>(kernel, "fetch", "deploy", deprecated: false);

            // Clone url
            routes.MapHandler<UploadPackHandler>(kernel, "git-upload-pack-root", "git-upload-pack", deprecated: false);
            routes.MapHandler<UploadPackHandler>(kernel, "git-upload-pack", configuration.GitServerRoot + "/git-upload-pack", deprecated: false);

            // Custom GIT repositories, which can be served from any directory that has a git repo
            routes.MapHandler<CustomGitRepositoryHandler>(kernel, "git-custom-repository", "git/{*path}", deprecated: false);

            // Scm (deployment repository)
            routes.MapHttpRouteDual("scm-info", "scm/info", new { controller = "LiveScm", action = "GetRepositoryInfo" });
            routes.MapHttpRouteDual("scm-clean", "scm/clean", new { controller = "LiveScm", action = "Clean" });
            routes.MapHttpRouteDual("scm-delete", "scm", new { controller = "LiveScm", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Scm files editor
            routes.MapHttpRouteDual("scm-get-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRouteDual("scm-put-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRouteDual("scm-delete-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "DeleteItem" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Live files editor
            routes.MapHttpRouteDual("vfs-get-files", "vfs/{*path}", new { controller = "Vfs", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRouteDual("vfs-put-files", "vfs/{*path}", new { controller = "Vfs", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRouteDual("vfs-delete-files", "vfs/{*path}", new { controller = "Vfs", action = "DeleteItem" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Zip file handler
            routes.MapHttpRouteDual("zip-get-files", "zip/{*path}", new { controller = "Zip", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRouteDual("zip-put-files", "zip/{*path}", new { controller = "Zip", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });

            // Live Command Line
            routes.MapHttpRouteDual("execute-command", "command", new { controller = "Command", action = "ExecuteCommand" }, new { verb = new HttpMethodConstraint("POST") });

            // Deployments
            routes.MapHttpRouteDual("all-deployments", "deployments", new { controller = "Deployment", action = "GetDeployResults" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("one-deployment-get", "deployments/{id}", new { controller = "Deployment", action = "GetResult" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("one-deployment-put", "deployments/{id}", new { controller = "Deployment", action = "Deploy", id = RouteParameter.Optional }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRouteDual("one-deployment-delete", "deployments/{id}", new { controller = "Deployment", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRouteDual("one-deployment-log", "deployments/{id}/log", new { controller = "Deployment", action = "GetLogEntry" });
            routes.MapHttpRouteDual("one-deployment-log-details", "deployments/{id}/log/{logId}", new { controller = "Deployment", action = "GetLogEntryDetails" });

            // Deployment script
            routes.MapHttpRoute("get-deployment-script", "api/deploymentscript", new { controller = "Deployment", action = "GetDeploymentScript" }, new { verb = new HttpMethodConstraint("GET") });

            // SSHKey
            routes.MapHttpRouteDual("get-sshkey", "sshkey", new { controller = "SSHKey", action = "GetPublicKey" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("put-sshkey", "sshkey", new { controller = "SSHKey", action = "SetPrivateKey" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRouteDual("delete-sshkey", "sshkey", new { controller = "SSHKey", action = "DeleteKeyPair" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Environment
            routes.MapHttpRouteDual("get-env", "environment", new { controller = "Environment", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });

            // Settings
            routes.MapHttpRouteDual("set-setting", "settings", new { controller = "Settings", action = "Set" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRoute("get-all-settings-old", "settings", new { controller = "Settings", action = "GetAll" }, new { verb = new HttpMethodConstraint("GET") }, deprecated: true);
            routes.MapHttpRoute("get-all-settings", "api/settings", new { controller = "Settings", action = "GetAll", version = 2 }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("get-setting", "settings/{key}", new { controller = "Settings", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("delete-setting", "settings/{key}", new { controller = "Settings", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Diagnostics
            routes.MapHttpRouteDual("diagnostics", "dump", new { controller = "Diagnostics", action = "GetLog" });
            routes.MapHttpRouteDual("diagnostics-set-setting", "diagnostics/settings", new { controller = "Diagnostics", action = "Set" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRouteDual("diagnostics-get-all-settings", "diagnostics/settings", new { controller = "Diagnostics", action = "GetAll" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("diagnostics-get-setting", "diagnostics/settings/{key}", new { controller = "Diagnostics", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("diagnostics-delete-setting", "diagnostics/settings/{key}", new { controller = "Diagnostics", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Logs
            routes.MapHandlerDual<LogStreamHandler>(kernel, "logstream", "logstream/{*path}");
            routes.MapHttpRoute("recent-logs", "api/logs/recent", new { controller = "Diagnostics", action = "GetRecentLogs" }, new { verb = new HttpMethodConstraint("GET") });

            // Processes
            routes.MapHttpProcessesRoute("all-processes", "", new { controller = "Process", action = "GetAllProcesses" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpProcessesRoute("one-process-get", "/{id}", new { controller = "Process", action = "GetProcess" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpProcessesRoute("one-process-delete", "/{id}", new { controller = "Process", action = "KillProcess" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpProcessesRoute("one-process-dump", "/{id}/dump", new { controller = "Process", action = "MiniDump" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpProcessesRoute("all-threads", "/{id}/threads", new { controller = "Process", action = "GetAllThreads" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpProcessesRoute("one-process-thread", "/{processId}/threads/{threadId}", new { controller = "Process", action = "GetThread" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpProcessesRoute("all-modules", "/{id}/modules", new { controller = "Process", action = "GetAllModules" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpProcessesRoute("one-process-module", "/{id}/modules/{baseAddress}", new { controller = "Process", action = "GetModule" }, new { verb = new HttpMethodConstraint("GET") });

            // Runtime
            routes.MapHttpRouteDual("runtime", "diagnostics/runtime", new { controller = "Runtime", action = "GetRuntimeVersions" }, new { verb = new HttpMethodConstraint("GET") });

            // Hooks
            routes.MapHttpRouteDual("unsubscribe-hook", "hooks/{id}", new { controller = "WebHooks", action = "Unsubscribe" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRouteDual("get-hook", "hooks/{id}", new { controller = "WebHooks", action = "GetWebHook" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("publish-hooks", "hooks/publish/{hookEventType}", new { controller = "WebHooks", action = "PublishEvent" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRouteDual("get-hooks", "hooks", new { controller = "WebHooks", action = "GetWebHooks" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRouteDual("subscribe-hook", "hooks", new { controller = "WebHooks", action = "Subscribe" }, new { verb = new HttpMethodConstraint("POST") });

            // Jobs
            routes.MapHttpWebJobsRoute("list-all-jobs", "", "", new { controller = "Jobs", action = "ListAllJobs" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("list-triggered-jobs", "triggered", "", new { controller = "Jobs", action = "ListTriggeredJobs" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("get-triggered-job", "triggered", "/{jobName}", new { controller = "Jobs", action = "GetTriggeredJob" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("invoke-triggered-job", "triggered", "/{jobName}/run", new { controller = "Jobs", action = "InvokeTriggeredJob" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpWebJobsRoute("get-triggered-job-history", "triggered", "/{jobName}/history", new { controller = "Jobs", action = "GetTriggeredJobHistory" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("get-triggered-job-run", "triggered", "/{jobName}/history/{runId}", new { controller = "Jobs", action = "GetTriggeredJobRun" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("create-triggered-job", "triggered", "/{jobName}", new { controller = "Jobs", action = "CreateTriggeredJob" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpWebJobsRoute("remove-triggered-job", "triggered", "/{jobName}", new { controller = "Jobs", action = "RemoveTriggeredJob" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpWebJobsRoute("get-triggered-job-settings", "triggered", "/{jobName}/settings", new { controller = "Jobs", action = "GetTriggeredJobSettings" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("set-triggered-job-settings", "triggered", "/{jobName}/settings", new { controller = "Jobs", action = "SetTriggeredJobSettings" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpWebJobsRoute("list-continuous-jobs", "continuous", "", new { controller = "Jobs", action = "ListContinuousJobs" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("get-continuous-job", "continuous", "/{jobName}", new { controller = "Jobs", action = "GetContinuousJob" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("disable-continuous-job", "continuous", "/{jobName}/stop", new { controller = "Jobs", action = "DisableContinuousJob" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpWebJobsRoute("enable-continuous-job", "continuous", "/{jobName}/start", new { controller = "Jobs", action = "EnableContinuousJob" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpWebJobsRoute("create-continuous-job", "continuous", "/{jobName}", new { controller = "Jobs", action = "CreateContinuousJob" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpWebJobsRoute("remove-continuous-job", "continuous", "/{jobName}", new { controller = "Jobs", action = "RemoveContinuousJob" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpWebJobsRoute("get-continuous-job-settings", "continuous", "/{jobName}/settings", new { controller = "Jobs", action = "GetContinuousJobSettings" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpWebJobsRoute("set-continuous-job-settings", "continuous", "/{jobName}/settings", new { controller = "Jobs", action = "SetContinuousJobSettings" }, new { verb = new HttpMethodConstraint("PUT") });

            // Web Jobs as microservice
            routes.MapHttpRoute("list-triggered-jobs-swagger", "api/triggeredwebjobsswagger", new { controller = "Jobs", action = "ListTriggeredJobsInSwaggerFormat" }, new { verb = new HttpMethodConstraint("GET") });

            // SiteExtensions
            routes.MapHttpRoute("api-get-remote-extensions", "api/extensionfeed", new { controller = "SiteExtension", action = "GetRemoteExtensions" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("api-get-remote-extension", "api/extensionfeed/{id}", new { controller = "SiteExtension", action = "GetRemoteExtension" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("api-get-local-extensions", "api/siteextensions", new { controller = "SiteExtension", action = "GetLocalExtensions" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("api-get-local-extension", "api/siteextensions/{id}", new { controller = "SiteExtension", action = "GetLocalExtension" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("api-uninstall-extension", "api/siteextensions/{id}", new { controller = "SiteExtension", action = "UninstallExtension" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRoute("api-install-update-extension", "api/siteextensions/{id}", new { controller = "SiteExtension", action = "InstallExtension" }, new { verb = new HttpMethodConstraint("PUT") });

            // catch all unregistered url to properly handle not found
            // this is to work arounf the issue in TraceModule where we see double OnBeginRequest call
            // for the same request (404 and then 200 statusCode).
            routes.MapHttpRoute("error-404", "{*path}", new { controller = "Error404", action = "Handle" });
        }

        // remove old LogFiles/Git trace
        private static void RemoveOldTracePath(IEnvironment environment)
        {
            FileSystemHelpers.DeleteDirectorySafe(Path.Combine(environment.LogFilesPath, "Git"), ignoreErrors: true);
        }

        private static void RemoveTempFileFromUserDrive(IEnvironment environment)
        {
            FileSystemHelpers.DeleteDirectorySafe(Path.Combine(environment.RootPath, "data", "Temp"), ignoreErrors: true);
        }

        // Perform migration tasks to deal with legacy sites that had different file layout
        private static void MigrateSite(IEnvironment environment, IDeploymentSettingsManager settings)
        {
            try
            {
                MoveOldSSHFolder(environment);
            }
            catch (Exception e)
            {
                ITracer tracer = GetTracerWithoutContext(environment, settings);
                tracer.Trace("Failed to move legacy .ssh folder: {0}", e.Message);
            }
        }

        // .ssh folder used to be under /site, and is now at the root
        private static void MoveOldSSHFolder(IEnvironment environment)
        {
            var oldSSHDirInfo = new DirectoryInfo(Path.Combine(environment.SiteRootPath, Constants.SSHKeyPath));

            if (oldSSHDirInfo.Exists)
            {
                string newSSHFolder = Path.Combine(environment.RootPath, Constants.SSHKeyPath);
                if (!Directory.Exists(newSSHFolder))
                {
                    Directory.CreateDirectory(newSSHFolder);
                }

                foreach (FileInfo file in oldSSHDirInfo.EnumerateFiles())
                {
                    // Copy the file to the new folder, unless it already exists
                    string newFile = Path.Combine(newSSHFolder, file.Name);
                    if (!File.Exists(newFile))
                    {
                        file.CopyTo(newFile, overwrite: true);
                    }
                }

                // Delete the old folder
                oldSSHDirInfo.Delete(recursive: true);
            }
        }

        private static void EnsureNpmGlobalDirectory()
        {
            try
            {
                string appData = System.Environment.GetEnvironmentVariable("APPDATA");
                FileSystemHelpers.EnsureDirectory(Path.Combine(appData, "npm"));

                // discovered while adding npm 2.1.17
                // this is to work around below issue with the very first npm install 
                // npm ERR! uid must be an unsigned int
                FileSystemHelpers.EnsureDirectory(Path.Combine(appData, "npm-cache"));
            }
            catch
            {
                // no op
            }
        }

        private static void EnsureUserProfileDirectory()
        {
            try
            {
                //this is for gulp 3.9.0 which fails if UserProfile is not there
                string userProfile = System.Environment.GetEnvironmentVariable("USERPROFILE");
                FileSystemHelpers.EnsureDirectory(userProfile);
            }
            catch
            {
                // no op
            }
        }

        private static ITracer GetTracer(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off && TraceServices.CurrentRequestTraceFile != null)
            {
                string textPath = Path.Combine(environment.TracePath, TraceServices.CurrentRequestTraceFile);
                return new CascadeTracer(new XmlTracer(environment.TracePath, level), new TextTracer(textPath, level));
            }

            return NullTracer.Instance;
        }

        private static ITracer GetTracerWithoutContext(IEnvironment environment, IDeploymentSettingsManager settings)
        {
            TraceLevel level = settings.GetTraceLevel();
            if (level > TraceLevel.Off)
            {
                return new XmlTracer(environment.TracePath, level);
            }

            return NullTracer.Instance;
        }

        private static void TraceShutdown(IEnvironment environment, IDeploymentSettingsManager settings)
        {
            ITracer tracer = GetTracerWithoutContext(environment, settings);
            var attribs = new Dictionary<string, string>();

            // Add an attribute containing the process, AppDomain and Thread ids to help debugging
            attribs.Add("pid", String.Format("{0},{1},{2}",
                Process.GetCurrentProcess().Id,
                AppDomain.CurrentDomain.Id.ToString(),
                System.Threading.Thread.CurrentThread.ManagedThreadId));

            attribs.Add("uptime", TraceModule.UpTime.ToString());

            attribs.Add("lastrequesttime", TraceModule.LastRequestTime.ToString());

            tracer.Trace(XmlTracer.ProcessShutdownTrace, attribs);
        }

        private static ILogger GetLogger(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off && TraceServices.CurrentRequestTraceFile != null)
            {
                string textPath = Path.Combine(environment.DeploymentTracePath, TraceServices.CurrentRequestTraceFile);
                return new TextLogger(textPath);
            }

            return NullLogger.Instance;
        }

        private static string GetRequestTraceFile(IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off)
            {
                return TraceServices.CurrentRequestTraceFile;
            }

            return null;
        }

        private static ICommandExecutor GetCommandExecutor(IEnvironment environment, IContext context)
        {
            if (System.String.IsNullOrEmpty(environment.RepositoryPath))
            {
                throw new HttpResponseException(HttpStatusCode.NotFound);
            }

            return new CommandExecutor(environment.RootPath, environment, context.Kernel.Get<IDeploymentSettingsManager>(), TraceServices.CurrentRequestTracer);
        }

        private static string GetSettingsPath(IEnvironment environment)
        {
            return Path.Combine(environment.DeploymentsPath, Constants.DeploySettingsPath);
        }

        private static void EnsureHomeEnvironmentVariable()
        {
            // If MapPath("/_app") returns a valid folder, set %HOME% to that, regardless of
            // it current value. This is the non-Azure code path.
            string path = HostingEnvironment.MapPath(Constants.MappedSite);
            if (Directory.Exists(path))
            {
                path = Path.GetFullPath(path);
                System.Environment.SetEnvironmentVariable("HOME", path);
            }
        }

        private static void EnsureSiteBitnessEnvironmentVariable()
        {
            if (System.Environment.GetEnvironmentVariable("SITE_BITNESS") == null)
            {
                System.Environment.SetEnvironmentVariable("SITE_BITNESS", System.Environment.Is64BitProcess ? Constants.X64Bit : Constants.X86Bit);
            }
        }

        private static IEnvironment GetEnvironment(IDeploymentSettingsManager settings = null)
        {
            string root = PathResolver.ResolveRootPath();
            string siteRoot = Path.Combine(root, Constants.SiteFolder);
            string repositoryPath = Path.Combine(siteRoot, settings == null ? Constants.RepositoryPath : settings.GetRepositoryPath());

            return new Kudu.Core.Environment(
                                   root,
                                   HttpRuntime.BinDirectory,
                                   repositoryPath);
        }
    }
}

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.SSHKey;
using Kudu.Core.Tracing;
using Kudu.Services.GitServer;
using Kudu.Services.Infrastructure;
using Kudu.Services.Performance;
using Kudu.Services.ServiceHookHandlers;
using Kudu.Services.SSHKey;
using Kudu.Services.Web.Infrastructure;
using Kudu.Services.Web.Services;
using Kudu.Services.Web.Tracing;
using Ninject;
using Ninject.Activation;
using Ninject.Web.Common;
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

        private static readonly Bootstrapper _bootstrapper = new Bootstrapper();

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

            IEnvironment environment = GetEnvironment();

            // Per request environment
            kernel.Bind<IEnvironment>().ToMethod(context => GetEnvironment(context.Kernel.Get<IDeploymentSettingsManager>()))
                                             .InRequestScope();

            // General
            kernel.Bind<HttpContextBase>().ToMethod(context => new HttpContextWrapper(HttpContext.Current))
                                             .InRequestScope();
            kernel.Bind<IServerConfiguration>().ToConstant(serverConfiguration);
            kernel.Bind<IFileSystem>().To<FileSystem>().InSingletonScope();

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

            var fileSystem = new FileSystem();
            var deploymentLock = new LockFile(deploymentLockPath, kernel.Get<ITraceFactory>(), fileSystem);
            var statusLock = new LockFile(statusLockPath, kernel.Get<ITraceFactory>(), fileSystem);
            var sshKeyLock = new LockFile(sshKeyLockPath, kernel.Get<ITraceFactory>(), fileSystem);
            var hooksLock = new LockFile(hooksLockPath, kernel.Get<ITraceFactory>(), fileSystem);

            kernel.Bind<IOperationLock>().ToConstant(sshKeyLock).WhenInjectedInto<SSHKeyController>();
            kernel.Bind<IOperationLock>().ToConstant(statusLock).WhenInjectedInto<DeploymentStatusManager>();
            kernel.Bind<IOperationLock>().ToConstant(hooksLock).WhenInjectedInto<WebHooksManager>();
            kernel.Bind<IOperationLock>().ToConstant(deploymentLock);

            var shutdownDetector = new ShutdownDetector();
            shutdownDetector.Initialize();

            // Trace shutdown event
            // Cannot use shutdownDetector.Token.Register because of race condition
            // with NinjectServices.Stop via WebActivator.ApplicationShutdownMethodAttribute
            Shutdown += () => TraceShutdown(environment, kernel);

            // LogStream service
            kernel.Bind<LogStreamManager>().ToMethod(context => new LogStreamManager(Path.Combine(environment.RootPath, Constants.LogFilesPath),
                                                                                     context.Kernel.Get<IEnvironment>(),
                                                                                     context.Kernel.Get<IDeploymentSettingsManager>(),
                                                                                     context.Kernel.Get<ITracer>(),
                                                                                     shutdownDetector));

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

            kernel.Bind<ILogger>().ToMethod(context => GetLogger(environment, context.Kernel))
                                             .InRequestScope();

            kernel.Bind<IRepository>().ToMethod(context => new GitExeRepository(context.Kernel.Get<IEnvironment>(),
                                                                                context.Kernel.Get<IDeploymentSettingsManager>(),
                                                                                context.Kernel.Get<ITraceFactory>()))
                                                .InRequestScope();

            kernel.Bind<IDeploymentManager>().To<DeploymentManager>()
                                             .InRequestScope();
            kernel.Bind<ISSHKeyManager>().To<SSHKeyManager>()
                                             .InRequestScope();

            kernel.Bind<IRepositoryFactory>().To<RepositoryFactory>()
                                             .InRequestScope();

            // Git server
            kernel.Bind<IDeploymentEnvironment>().To<DeploymentEnvrionment>();

            kernel.Bind<IGitServer>().ToMethod(context => new GitExeServer(context.Kernel.Get<IEnvironment>(),
                                                                           deploymentLock,
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
            GlobalConfiguration.Configuration.Filters.Add(new TraceExceptionFilterAttribute());

            // Git Service
            routes.MapHttpRoute("git-info-refs-root", "info/refs", new { controller = "InfoRefs", action = "Execute" });
            routes.MapHttpRoute("git-info-refs", configuration.GitServerRoot + "/info/refs", new { controller = "InfoRefs", action = "Execute" });

            // Push url
            routes.MapHandler<ReceivePackHandler>(kernel, "git-receive-pack-root", "git-receive-pack");
            routes.MapHandler<ReceivePackHandler>(kernel, "git-receive-pack", configuration.GitServerRoot + "/git-receive-pack");

            // Fetch Hook
            routes.MapHandler<FetchHandler>(kernel, "fetch", "deploy");

            // Clone url
            routes.MapHandler<UploadPackHandler>(kernel, "git-upload-pack-root", "git-upload-pack");
            routes.MapHandler<UploadPackHandler>(kernel, "git-upload-pack", configuration.GitServerRoot + "/git-upload-pack");

            // Custom GIT repositories, which can be served from any directory that has a git repo
            routes.MapHandler<CustomGitRepositoryHandler>(kernel, "git-custom-repository", "git/{*path}");

            // Scm (deployment repository)
            routes.MapHttpRoute("scm-info", "scm/info", new { controller = "LiveScm", action = "GetRepositoryInfo" });
            routes.MapHttpRoute("scm-clean", "scm/clean", new { controller = "LiveScm", action = "Clean" });
            routes.MapHttpRoute("scm-delete", "scm", new { controller = "LiveScm", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Scm files editor
            routes.MapHttpRoute("scm-get-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRoute("scm-put-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("scm-delete-files", "scmvfs/{*path}", new { controller = "LiveScmEditor", action = "DeleteItem" }, new { verb = new HttpMethodConstraint("DELETE") });

            // These older scm routes are there for backward compat, and should eventually be deleted once clients are changed.
            routes.MapHttpRoute("live-scm-info", "live/scm/info", new { controller = "LiveScm", action = "GetRepositoryInfo" });
            routes.MapHttpRoute("live-scm-clean", "live/scm/clean", new { controller = "LiveScm", action = "Clean" });
            routes.MapHttpRoute("live-scm-delete", "live/scm", new { controller = "LiveScm", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Live files editor
            routes.MapHttpRoute("vfs-get-files", "vfs/{*path}", new { controller = "Vfs", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRoute("vfs-put-files", "vfs/{*path}", new { controller = "Vfs", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("vfs-delete-files", "vfs/{*path}", new { controller = "Vfs", action = "DeleteItem" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Zip file handler
            routes.MapHttpRoute("zip-get-files", "zip/{*path}", new { controller = "Zip", action = "GetItem" }, new { verb = new HttpMethodConstraint("GET", "HEAD") });
            routes.MapHttpRoute("zip-put-files", "zip/{*path}", new { controller = "Zip", action = "PutItem" }, new { verb = new HttpMethodConstraint("PUT") });

            // Live Command Line
            routes.MapHttpRoute("execute-command", "command", new { controller = "Command", action = "ExecuteCommand" }, new { verb = new HttpMethodConstraint("POST") });

            // Deployments
            routes.MapHttpRoute("all-deployments", "deployments", new { controller = "Deployment", action = "GetDeployResults" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("one-deployment-get", "deployments/{id}", new { controller = "Deployment", action = "GetResult" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("one-deployment-put", "deployments/{id}", new { controller = "Deployment", action = "Deploy", id = RouteParameter.Optional }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("one-deployment-delete", "deployments/{id}", new { controller = "Deployment", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRoute("one-deployment-log", "deployments/{id}/log", new { controller = "Deployment", action = "GetLogEntry" });
            routes.MapHttpRoute("one-deployment-log-details", "deployments/{id}/log/{logId}", new { controller = "Deployment", action = "GetLogEntryDetails" });

            // SSHKey
            routes.MapHttpRoute("get-sshkey", "sshkey", new { controller = "SSHKey", action = "GetPublicKey" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("put-sshkey", "sshkey", new { controller = "SSHKey", action = "SetPrivateKey" }, new { verb = new HttpMethodConstraint("PUT") });
            routes.MapHttpRoute("delete-sshkey", "sshkey", new { controller = "SSHKey", action = "DeleteKeyPair" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Environment
            routes.MapHttpRoute("get-env", "environment", new { controller = "Environment", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });

            // Settings
            routes.MapHttpRoute("set-setting", "settings", new { controller = "Settings", action = "Set" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRoute("get-all-settings", "settings", new { controller = "Settings", action = "GetAll" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("get-setting", "settings/{key}", new { controller = "Settings", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("delete-setting", "settings/{key}", new { controller = "Settings", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // Diagnostics
            routes.MapHttpRoute("diagnostics", "dump", new { controller = "Diagnostics", action = "GetLog" });
            routes.MapHttpRoute("diagnostics-set-setting", "diagnostics/settings", new { controller = "Diagnostics", action = "Set" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRoute("diagnostics-get-all-settings", "diagnostics/settings", new { controller = "Diagnostics", action = "GetAll" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("diagnostics-get-setting", "diagnostics/settings/{key}", new { controller = "Diagnostics", action = "Get" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("diagnostics-delete-setting", "diagnostics/settings/{key}", new { controller = "Diagnostics", action = "Delete" }, new { verb = new HttpMethodConstraint("DELETE") });

            // LogStream
            routes.MapHandler<LogStreamHandler>(kernel, "logstream", "logstream/{*path}");

            // Processes
            routes.MapHttpRoute("all-processes", "diagnostics/processes", new { controller = "Process", action = "GetAllProcesses" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("one-process-get", "diagnostics/processes/{id}", new { controller = "Process", action = "GetProcess" }, new { verb = new HttpMethodConstraint("GET") });
            routes.MapHttpRoute("one-process-delete", "diagnostics/processes/{id}", new { controller = "Process", action = "KillProcess" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRoute("one-process-dump", "diagnostics/processes/{id}/dump", new { controller = "Process", action = "MiniDump" }, new { verb = new HttpMethodConstraint("GET") });

            // Hooks
            routes.MapHttpRoute("subscribe-hook", "hooks/subscribe", new { controller = "WebHooks", action = "Subscribe" }, new { verb = new HttpMethodConstraint("POST") });
            routes.MapHttpRoute("unsubscribe-hook", "hooks/unsubscribe", new { controller = "WebHooks", action = "Unsubscribe" }, new { verb = new HttpMethodConstraint("DELETE") });
            routes.MapHttpRoute("get-hooks", "hooks", new { controller = "WebHooks", action = "GetWebHooks" }, new { verb = new HttpMethodConstraint("GET") });
        }

        private static ITracer GetTracer(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off)
            {
                string tracePath = Path.Combine(environment.TracePath, Constants.TraceFile);
                string textPath = Path.Combine(environment.TracePath, TraceServices.CurrentRequestTraceFile);
                string traceLockPath = Path.Combine(environment.TracePath, Constants.TraceLockFile);
                var traceLock = new LockFile(traceLockPath);
                return new CascadeTracer(new Tracer(tracePath, level, traceLock), new TextTracer(textPath, level));
            }

            return NullTracer.Instance;
        }

        private static void TraceShutdown(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off)
            {
                string tracePath = Path.Combine(environment.TracePath, Constants.TraceFile);
                string traceLockPath = Path.Combine(environment.TracePath, Constants.TraceLockFile);
                var traceLock = new LockFile(traceLockPath);
                ITracer tracer = new Tracer(tracePath, level, traceLock);
                var attribs = new Dictionary<string, string>();

                // Add an attribute containing the process, AppDomain and Thread ids to help debugging
                attribs.Add("pid", String.Format("{0},{1},{2}",
                    Process.GetCurrentProcess().Id,
                    AppDomain.CurrentDomain.Id.ToString(),
                    System.Threading.Thread.CurrentThread.ManagedThreadId));

                attribs.Add("uptime", TraceModule.UpTime.ToString());

                attribs.Add("lastrequesttime", TraceModule.LastRequestTime.ToString());

                tracer.Trace("Process Shutdown", attribs);
            }
        }

        private static ILogger GetLogger(IEnvironment environment, IKernel kernel)
        {
            TraceLevel level = kernel.Get<IDeploymentSettingsManager>().GetTraceLevel();
            if (level > TraceLevel.Off)
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

        private static IEnvironment GetEnvironment(IDeploymentSettingsManager settings = null)
        {
            string siteRoot = PathResolver.ResolveSiteRootPath();
            string root = Path.GetFullPath(Path.Combine(siteRoot, ".."));
            string webRootPath = Path.Combine(siteRoot, Constants.WebRoot);
            string deployCachePath = Path.Combine(siteRoot, Constants.DeploymentCachePath);
            string diagnosticsPath = Path.Combine(siteRoot, Constants.DiagnosticsPath);
            string sshKeyPath = Path.Combine(siteRoot, Constants.SSHKeyPath);
            string repositoryPath = Path.Combine(siteRoot, settings == null ? Constants.RepositoryPath : settings.GetRepositoryPath());
            string tempPath = Path.GetTempPath();
            string scriptPath = Path.Combine(HttpRuntime.BinDirectory, Constants.ScriptsPath);
            string nodeModulesPath = Path.Combine(HttpRuntime.BinDirectory, Constants.NodeModulesPath);

            return new Kudu.Core.Environment(
                                   new FileSystem(),
                                   root,
                                   siteRoot,
                                   tempPath,
                                   repositoryPath,
                                   webRootPath,
                                   deployCachePath,
                                   diagnosticsPath,
                                   sshKeyPath,
                                   scriptPath,
                                   nodeModulesPath);
        }
    }
}
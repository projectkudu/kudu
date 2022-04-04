using System;
using Kudu.Core.SourceControl;
using Kudu.Contracts.Tracing;
using System.Threading.Tasks;
using Kudu.Contracts.Deployment;

namespace Kudu.Core.Deployment
{
    public abstract class DeploymentInfoBase
    {
        public delegate Task FetchDelegate(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer);

        protected DeploymentInfoBase()
        {
            IsReusable = true;
            AllowDeferredDeployment = true;
            DoFullBuildByDefault = true;
            WatchedFileEnabled = true;
            RestartAllowed = true;
            ArtifactType = ArtifactType.Unknown;
        }

        public RepositoryType RepositoryType { get; set; }
        public string RepositoryUrl { get; set; }
        public string Deployer { get; set; }
        public ChangeSet TargetChangeset { get; set; }
        public bool IsReusable { get; set; }
        // Allow deferred deployment via marker file mechanism.
        public bool AllowDeferredDeployment { get; set; }
        // indicating that this is a CI triggered by SCM provider
        public bool IsContinuous { get; set; }

        // NOTE: Do not access the request stream in the Fetch handler as it may have been closed during asynchronous scenarios
        public FetchDelegate Fetch { get; set; }
        public bool AllowDeploymentWhileScmDisabled { get; set; }

        // Optional.
        // By default, TargetSubDirectoryRelativePath specifies the directory to deploy to relative to /home/site/wwwroot.
        // This property can be used to change the root from wwwroot to something else.
        public string TargetRootPath { get; set; }

        // Optional.
        // Path of the directory to be deployed to. The path should be relative to the wwwroot directory.
        // Example: "webapps/ROOT"
        public string TargetSubDirectoryRelativePath { get; set; }

        // Optional.
        // Specifies the name of the deployed artifact.
        // Example: When deploying startup files, OneDeploy will set this to startup.cmd (or startup.sh)
        public string TargetFileName { get; set; }

        // Optional.
        // Type of artifact being deployed.
        public ArtifactType ArtifactType { get; set; }

        // Optional.
        // Path of the file that is watched for changes by the web server.
        // The path must be relative to the directory where deployment is performed.
        //
        // Example1: If SCM_TARGET_PATH is not defined, WatchedFilePath is "web.config",
        // file to be touched would refer to "%HOME%\site\wwwroot\web.config".
        // Example2: If SCM_TARGET_PATH is "dir1", WatchedFilePath is "dir2/web.xml",
        // file to be touched would refer to "%HOME%\site\wwwroot\dir1\dir2\web.xml".
        public string WatchedFilePath { get; set; }

        // this is only set by GenericHandler
        // the RepositoryUrl can specify specific commitid to deploy
        // for instance, http://github.com/kuduapps/hellokudu.git#<commitid>
        public string CommitId { get; set; }

        public bool CleanupTargetDirectory { get; set; }

        // Can set to false for deployments where we assume that the repository contains the entire
        // built site, meaning we can skip app stack detection and simply use BasicBuilder
        // (KuduSync only)
        public bool DoFullBuildByDefault { get; set; }

        public bool IsValid()
        {
            return !String.IsNullOrEmpty(Deployer);
        }

        public abstract IRepository GetRepository();

        // If this is not set, sync triggers will look under d:\home\site\wwwroot
        // for functionsRoot. Otherwise it'll use this path for that
        // This is used in Run-From-Zip deployments where the content of wwwroot
        // won't update until after a process restart. Therefore, we copy the needed
        // files into a separate folders and run sync triggers from there.
        public string SyncFunctionsTriggersPath { get; set; } = null;

        // Allows the use of a caller-provided GUID for the deployment, rather than
        // a commit hash or a randomly-generated identifier.
        public string DeploymentTrackingId { get; set; } = null;

        // Specifies whether to touch the watched file (example web.config, web.xml, etc) after the deployment
        public bool WatchedFileEnabled { get; set;}

        // Used to allow / disallow 'restart' on a per deployment basis, if needed.
        // For example: OneDeploy allows clients to enable / disable 'restart'.
        public bool RestartAllowed { get; set; }

        /* capture x-ms-correlation-id is passed in request*/
        public string CorrelationId { get; set; }

        /* capture deployment path*/
        public string DeploymentPath { get; set; }
    }
}
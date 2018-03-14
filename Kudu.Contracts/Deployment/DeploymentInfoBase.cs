using System;
using Kudu.Core.SourceControl;
using Kudu.Contracts.Tracing;
using System.Threading.Tasks;

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
        public FetchDelegate Fetch { get; set; }
        public bool AllowDeploymentWhileScmDisabled { get; set; }

        // Optional.
        // Path of the directory to be deployed to. The path should be relative to the wwwroot directory.
        // Example: "webapps/ROOT"
        public string TargetPath { get; set; }

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
    }
}
using System;
using Kudu.Core.SourceControl;
using Kudu.Contracts.Tracing;
using System.Threading.Tasks;
using Kudu.Core.Deployment;

namespace Kudu.Services.ServiceHookHandlers
{
    public class DeploymentInfo
    {
        public delegate Task FetchDelegate(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch, ILogger logger, ITracer tracer);

        public DeploymentInfo()
        {
            IsReusable = true;
        }

        public RepositoryType RepositoryType { get; set; }
        public string RepositoryUrl { get; set; }
        public string Deployer { get; set; }
        public ChangeSet TargetChangeset { get; set; }
        public bool IsReusable { get; set; }
        // indicating that this is a CI triggered by SCM provider 
        public bool IsContinuous { get; set; }
        public FetchDelegate Fetch { get; set; }
        public bool AllowDeploymentWhileScmDisabled { get; set; }

        // this is only set by GenericHandler
        // the RepositoryUrl can specify specific commitid to deploy
        // for instance, http://github.com/kuduapps/hellokudu.git#<commitid>
        public string CommitId { get; set; }

        public bool IsValid()
        {
            return !String.IsNullOrEmpty(Deployer);
        }
    }
}
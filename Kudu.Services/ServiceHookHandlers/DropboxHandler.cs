using System;
using System.Web;
using Kudu.Contracts.Dropbox;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class DropboxHandler : IServiceHookHandler
    {
        protected readonly ITracer _tracer;
        protected readonly DropboxHelper _dropBoxHelper;
        protected readonly IDeploymentSettingsManager _settings;

        public DropboxHandler(ITracer tracer,
                              IServerRepository repository,
                              IDeploymentSettingsManager settings,
                              IEnvironment environment)
        {
            _tracer = tracer;
            _settings = settings;
            _dropBoxHelper = new DropboxHelper(tracer, repository, settings, environment);
        }

        public DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;
            if (!String.IsNullOrEmpty(payload.Value<string>("NewCursor")))
            {
                var dropboxInfo = new DropboxInfo(payload);
                deploymentInfo = dropboxInfo; 

                // Temporary deployment
                deploymentInfo.TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(
                    authorName: dropboxInfo.DeployInfo.UserName,
                    authorEmail: dropboxInfo.DeployInfo.Email,
                    message: "Syncing with dropbox at " + DateTime.UtcNow.ToString("g"));

                return DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        public virtual void Fetch(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch)
        {
            // Sync with dropbox
            var dropboxInfo = ((DropboxInfo)deploymentInfo);
            deploymentInfo.TargetChangeset = _dropBoxHelper.Sync(dropboxInfo.DeployInfo, targetBranch);
        }

        internal class DropboxInfo : DeploymentInfo
        {
            public DropboxInfo(JObject payload)
            {
                Deployer = DropboxHelper.Dropbox;
                DeployInfo = payload.ToObject<DropboxDeployInfo>();
                // This ensure that the fetch handler provides an underlying Git repository.
                RepositoryType = RepositoryType.Git;
            }

            public DropboxDeployInfo DeployInfo { get; set; }
        }
    }
}
using System;
using System.Web;
using Kudu.Contracts.Dropbox;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
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
                deploymentInfo = new DropboxInfo(payload);
                
                // Temporary deployment
                string authorName = _settings.GetValue(DropboxHelper.UserNameKey) ?? _settings.GetGitUsername();
                string authorEmail = _settings.GetValue(DropboxHelper.EmailKey) ?? _settings.GetGitEmail();
                string message = "Syncing with dropbox at " + DateTime.UtcNow.ToString("g");
                deploymentInfo.TargetChangeset = new ChangeSet("InProgress", authorName, authorEmail, message, DateTimeOffset.MinValue);

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

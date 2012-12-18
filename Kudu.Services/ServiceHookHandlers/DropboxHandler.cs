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

        public DropboxHandler(ITracer tracer,
                              IServerRepository repository,
                              IDeploymentSettingsManager settings,
                              IEnvironment environment)
        {
            _tracer = tracer;
            _dropBoxHelper = new DropboxHelper(tracer, repository, settings, environment);
        }

        public DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;
            if (!String.IsNullOrEmpty(payload.Value<string>("NewCursor")))
            {
                deploymentInfo = new DropboxInfo(payload);
                return DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        public bool RequiresProcessing(DeploymentInfo deploymentInfo, string targetBranch)
        {
            return true;
        }

        public virtual void Fetch(DeploymentInfo deploymentInfo, string targetBranch)
        {
            // Sync with dropbox
            var dropboxInfo = ((DropboxInfo)deploymentInfo);
            _dropBoxHelper.Sync(dropboxInfo.DeployInfo, targetBranch);
        }

        internal class DropboxInfo : DeploymentInfo
        {
            public DropboxInfo(JObject payload)
            {
                Deployer = DropboxHelper.Dropbox;
                DeployInfo = payload.ToObject<DropboxDeployInfo>();
            }

            public DropboxDeployInfo DeployInfo { get; set; }
        }
    }
}

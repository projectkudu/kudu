using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using Kudu.Contracts.Dropbox;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.SourceControl;
using Kudu.Services.Dropbox;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class DropboxHandler : IServiceHookHandler
    {
        protected readonly ITracer _tracer;
        protected readonly DropboxHelper _helper;

        public DropboxHandler(ITracer tracer,
                              IServerRepository repository,
                              IDeploymentSettingsManager settings,
                              IEnvironment environment)
        {
            _tracer = tracer;
            _helper = new DropboxHelper(tracer, repository, settings, environment);
        }

        public bool TryGetRepositoryInfo(HttpRequest request, JObject payload, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;
            if (!String.IsNullOrEmpty(payload.Value<string>("NewCursor")))
            {
                repositoryInfo = new DropboxInfo(payload);
            }

            return repositoryInfo != null;
        }

        public virtual void Fetch(RepositoryInfo repositoryInfo, string targetBranch)
        {
            // Sync with dropbox
            DropboxInfo info = (DropboxInfo)repositoryInfo;

            _helper.Sync(info.DeployInfo, targetBranch);
        }

        private class DropboxInfo : RepositoryInfo
        {
            public DropboxInfo(JObject payload)
            {
                Deployer = DropboxHelper.Dropbox;
                NewRef = "dummy";
                OldRef = "dummy";
                DeployInfo = payload.ToObject<DropboxDeployInfo>();
            }

            public DropboxDeployInfo DeployInfo { get; set; }
        }
    }
}
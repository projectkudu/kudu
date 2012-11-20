using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Kudu.Contracts.Dropbox;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.SourceControl;
using Kudu.Services.Dropbox;
using Newtonsoft.Json;

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

        public bool TryGetRepositoryInfo(HttpRequest request, out RepositoryInfo repositoryInfo)
        {
            repositoryInfo = null;
            if (request.UserAgent != null &&
                request.UserAgent.StartsWith(DropboxHelper.Dropbox , StringComparison.OrdinalIgnoreCase))
            {
                DropboxInfo info = DropboxInfo.Parse(request.GetInputStream());

                _tracer.Trace("Dropbox payload", new Dictionary<string, string>
                {
                    { "changes", info.DeployInfo.Deltas.Count().ToString() },
                    { "oldcursor", info.DeployInfo.OldCursor },
                    { "newcursor", info.DeployInfo.NewCursor }
                });

                repositoryInfo = info;
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
            public DropboxDeployInfo DeployInfo { get; set; }

            public static DropboxInfo Parse(Stream stream)
            {
                DropboxDeployInfo deployInfo = null;
                using (JsonTextReader reader = new JsonTextReader(new StreamReader(stream)))
                {
                    JsonSerializer serializer = new JsonSerializer();
                    try
                    {
                        deployInfo = serializer.Deserialize<DropboxDeployInfo>(reader);
                    }
                    catch (Exception ex)
                    {
                        throw new FormatException(Resources.Error_UnsupportedFormat, ex);
                    }

                    if (deployInfo == null)
                    {
                        throw new FormatException(Resources.Error_EmptyPayload);
                    }
                }

                return new DropboxInfo
                {
                    Deployer = DropboxHelper.Dropbox,
                    NewRef = "dummy",
                    OldRef = "dummy",
                    DeployInfo = deployInfo
                };
            }
        }
    }
}
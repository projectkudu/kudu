using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;
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
        private const string DropboxTokenKey = "dropbox_token";
        private const string DropboxPathKey = "dropbox_path";
        private readonly DropboxHelper _dropBoxHelper;
        private readonly IDeploymentSettingsManager _settings;

        public DropboxHandler(ITracer tracer,
                              IDeploymentStatusManager status,
                              IDeploymentSettingsManager settings,
                              IEnvironment environment)
        {
            _settings = settings;
            _dropBoxHelper = new DropboxHelper(tracer, status, settings, environment);
        }

        public DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            DropboxInfo dropboxInfo = null;
            string message = null;

            if (!String.IsNullOrEmpty(payload.Value<string>("NewCursor")))
            {
                dropboxInfo = DropboxInfo.CreateV1Info(payload, GetRepositoryType());
                message = String.Format(CultureInfo.CurrentUICulture, Resources.Dropbox_SynchronizingNChanges, dropboxInfo.DeployInfo.Deltas.Count);
            }
            else if (String.Equals(payload.Value<string>("scmType"), "DropboxV2", StringComparison.OrdinalIgnoreCase))
            {
                string oauthToken = GetValue(payload, DropboxTokenKey),
                       path = GetValue(payload, DropboxPathKey),
                       userName = payload.Value<string>("dropbox_username") ?? "Dropbox",
                       email = payload.Value<string>("dropbox_email");
                
                dropboxInfo = DropboxInfo.CreateV2Info(path, oauthToken, GetRepositoryType());
                dropboxInfo.DeployInfo.UserName = userName;
                dropboxInfo.DeployInfo.Email = email;
                message = String.Format(CultureInfo.CurrentUICulture, Resources.Dropbox_Synchronizing);
            }

            if (dropboxInfo != null)
            {
                deploymentInfo = dropboxInfo;

                // Temporary deployment
                deploymentInfo.TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(
                    authorName: dropboxInfo.DeployInfo.UserName,
                    authorEmail: dropboxInfo.DeployInfo.Email,
                    message: message
                );
                
                return DeployAction.ProcessDeployment;
            }

            deploymentInfo = null;
            return DeployAction.UnknownPayload;
        }

        public virtual async Task Fetch(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch, ILogger logger)
        {
            // (A)sync with dropbox
            var dropboxInfo = (DropboxInfo)deploymentInfo;
            _dropBoxHelper.Logger = logger;
            deploymentInfo.TargetChangeset = await _dropBoxHelper.Sync(dropboxInfo, targetBranch, repository);
        }

        private RepositoryType GetRepositoryType()
        {
            return _settings.IsNullRepository() ? RepositoryType.None : RepositoryType.Git;
        }

        private string GetValue(JObject payload, string key)
        {
            string value = payload.Value<string>(key) ?? _settings.GetValue(key);

            if (String.IsNullOrEmpty(value))
            {
                throw new InvalidOperationException(String.Format(CultureInfo.CurrentCulture, Resources.Error_DropboxValueNotSpecified, key));
            }

            return value;
        }
    }
}
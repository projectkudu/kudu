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
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    public class DropboxHandler : IServiceHookHandler
    {
        private const string DropboxVersionKey = "dropbox_version";
        private const string DropboxTokenKey = "dropbox_token";
        private const string DropboxPathKey = "dropbox_path";
        private readonly DropboxHelper _dropBoxHelper;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IRepositoryFactory _repositoryFactory;

        public DropboxHandler(ITracer tracer,
                              IDeploymentStatusManager status,
                              IDeploymentSettingsManager settings,
                              IEnvironment environment,
                              IRepositoryFactory repositoryFactory)
        {
            _settings = settings;
            _repositoryFactory = repositoryFactory;
            _dropBoxHelper = new DropboxHelper(tracer, status, settings, environment);
        }

        public DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfoBase deploymentInfo)
        {
            DropboxInfo dropboxInfo = null;
            string message = null;

            if (!String.IsNullOrEmpty(payload.Value<string>("NewCursor")))
            {
                dropboxInfo = DropboxInfo.CreateV1Info(payload, GetRepositoryType(), _repositoryFactory);
                message = String.Format(CultureInfo.CurrentUICulture, Resources.Dropbox_SynchronizingNChanges, dropboxInfo.DeployInfo.Deltas.Count);
            }
            else if (String.Equals(payload.Value<string>(DropboxVersionKey), "2", StringComparison.OrdinalIgnoreCase))
            {
                string oauthToken = GetValue(payload, DropboxTokenKey),
                       path = GetValue(payload, DropboxPathKey),
                       userName = payload.Value<string>("dropbox_username") ?? "Dropbox",
                       email = payload.Value<string>("dropbox_email");
                
                dropboxInfo = DropboxInfo.CreateV2Info(path, oauthToken, GetRepositoryType(), _repositoryFactory);
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

                deploymentInfo.AllowDeploymentWhileScmDisabled = true;
                
                return DeployAction.ProcessDeployment;
            }

            deploymentInfo = null;
            return DeployAction.UnknownPayload;
        }

        public virtual async Task Fetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            // (A)sync with dropbox
            var dropboxInfo = (DropboxInfo)deploymentInfo;
            _dropBoxHelper.Logger = logger;
            deploymentInfo.TargetChangeset = await _dropBoxHelper.Sync(dropboxInfo, targetBranch, repository, tracer);
        }

        private RepositoryType GetRepositoryType()
        {
            return _settings.NoRepository() ? RepositoryType.None : RepositoryType.Git;
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
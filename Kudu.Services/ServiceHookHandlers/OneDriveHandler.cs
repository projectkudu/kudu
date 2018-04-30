using System;
using System.Globalization;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Services.FetchHelpers;
using Newtonsoft.Json.Linq;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    public class OneDriveHandler : IServiceHookHandler
    {
        private OneDriveHelper _oneDriveHelper { get; set; }

        private IDeploymentSettingsManager _settings;
        private IRepositoryFactory _repositoryFactory;

        public OneDriveHandler(ITracer tracer,
                               IDeploymentStatusManager status,
                               IDeploymentSettingsManager settings,
                               IEnvironment environment,
                               IRepositoryFactory repositoryFactory)
        {
            _settings = settings;
            _repositoryFactory = repositoryFactory;
            _oneDriveHelper = new OneDriveHelper(tracer, status, settings, environment);
        }

        public DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfoBase deploymentInfo)
        {
            deploymentInfo = null;
            string url = payload.Value<string>("RepositoryUrl");
            if (string.IsNullOrWhiteSpace(url) || !url.ToLowerInvariant().Contains("api.onedrive.com"))
            {
                return DeployAction.UnknownPayload;
            }

            /*
                 Expecting payload to be:
                 {
                    "RepositoryUrl": "xxx",
                    "AccessToken": "xxx"
                 }
             */
            string accessToken = payload.Value<string>("AccessToken");

            // keep email and name, so that can be re-used in later commit
            OneDriveInfo oneDriveInfo = new OneDriveInfo(_repositoryFactory)
            {
                Deployer = "OneDrive",
                RepositoryUrl = url,
                AccessToken = accessToken,
                AuthorName = _settings.GetValue("authorName"),
                AuthorEmail = _settings.GetValue("authorEmail")
            };

            deploymentInfo = oneDriveInfo;

            deploymentInfo.TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(
                authorName: oneDriveInfo.AuthorName,
                authorEmail: oneDriveInfo.AuthorEmail,
                message: String.Format(CultureInfo.CurrentUICulture, Resources.OneDrive_Synchronizing)
            );

            return DeployAction.ProcessDeployment;
        }

        public async Task Fetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            var oneDriveInfo = (OneDriveInfo)deploymentInfo;
            _oneDriveHelper.Logger = logger;
            oneDriveInfo.TargetChangeset = await _oneDriveHelper.Sync(oneDriveInfo, repository, tracer);
        }
    }
}

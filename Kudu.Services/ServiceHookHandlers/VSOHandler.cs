using System;
using System.Globalization;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;
using Kudu.Core.Deployment;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Default Servicehook Handler, uses VSO format.
    /// </summary>
    public class VSOHandler : ServiceHookHandlerBase
    {
        private IDeploymentSettingsManager _settings;

        public VSOHandler(IDeploymentSettingsManager settings, IRepositoryFactory repositoryFactory)
            : base(repositoryFactory)
        {
            _settings = settings;
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfoBase deploymentInfo)
        {
            deploymentInfo = null;

            var publisherId = payload.Value<string>("publisherId");
            if (String.Equals(publisherId, "tfs", StringComparison.OrdinalIgnoreCase))
            {
                deploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                return DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        protected virtual DeploymentInfoBase GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            var sessionToken = payload.Value<JObject>("sessionToken");

            var info = new DeploymentInfo(RepositoryFactory)
            {
                RepositoryType = RepositoryType.Git,
                Deployer = GetDeployer(),
                IsContinuous = true
            };

            // even it is empty password we need to explicitly say so (with colon).
            // without colon, LibGit2Sharp is not working
            Uri remoteUrl = GetRemoteUrl(payload);
            if (sessionToken == null)
            {
                // if there is no session token, fallback to use raw remoteUrl from setting.xml
                info.RepositoryUrl = remoteUrl.AbsoluteUri;
            }
            else
            {
                info.RepositoryUrl = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:@{2}{3}",
                    remoteUrl.Scheme,
                    sessionToken.Value<string>("token"),
                    remoteUrl.Authority,
                    remoteUrl.PathAndQuery);
            }

            info.TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(
                authorName: info.Deployer,
                authorEmail: info.Deployer,
                message: String.Format(CultureInfo.CurrentUICulture, Resources.Vso_Synchronizing)
            );

            return info;
        }

        protected virtual string GetDeployer()
        {
            return "VSTS";
        }

        private Uri GetRemoteUrl(JObject payload)
        {
            var remoteUrl = _settings.GetValue("RepoUrl");
            if (String.IsNullOrEmpty(remoteUrl))
            {
                var resource = payload.Value<JObject>("resource");
                var repository = resource.Value<JObject>("repository");
                remoteUrl = repository.Value<string>("remoteUrl");
            }

            return new Uri(remoteUrl);
        }
    }
}

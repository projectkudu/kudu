using System;
using System.Collections.Generic;
using System.Globalization;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// Default Servicehook Handler, uses VSO format.
    /// </summary>
    public class VSOHandler : ServiceHookHandlerBase
    {
        private IDeploymentSettingsManager _settings;

        public VSOHandler(IDeploymentSettingsManager settings)
        {
            _settings = settings;
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
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

        protected virtual GitDeploymentInfo GetDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch)
        {
            var sessionToken = payload.Value<JObject>("sessionToken");

            var info = new GitDeploymentInfo { RepositoryType = RepositoryType.Git };
            info.Deployer = GetDeployer();

            // even it is empty password we need to explicitly say so (with colon).
            // without colon, LibGit2Sharp is not working
            Uri remoteUrl = new Uri(_settings.GetValue("repoUrl"));
            info.RepositoryUrl = string.Format(CultureInfo.InvariantCulture, "{0}://{1}:@{2}{3}",
                remoteUrl.Scheme,
                sessionToken.Value<string>("token"),
                remoteUrl.Authority,
                remoteUrl.PathAndQuery);

            info.TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(
                authorName: info.Deployer,
                authorEmail: info.Deployer,
                message: String.Format(CultureInfo.CurrentUICulture, Resources.Vso_Synchronizing)
            );

            return info;
        }

        protected virtual string GetDeployer()
        {
            return "VSO";
        }
    }
}

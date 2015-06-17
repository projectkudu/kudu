using System;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Services.FetchHelpers;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class OneDriveHandler : IServiceHookHandler
    {
        private readonly OneDriveHelper _oneDriveHelper;

        public OneDriveHandler(ITracer tracer,
                              IDeploymentSettingsManager settings,
                              IEnvironment environment)
        {
            _oneDriveHelper = new OneDriveHelper(tracer, settings, environment);
        }

        public DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
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
            deploymentInfo = new OneDriveInfo()
            {
                Deployer = "OneDrive",
                RepositoryUrl = url,
                AccessToken = payload.Value<string>("AccessToken")
            };

            return DeployAction.ProcessDeployment;
        }

        public async Task Fetch(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch, ILogger logger)
        {
            var oneDriveInfo = (OneDriveInfo)deploymentInfo;
            _oneDriveHelper.Logger = logger;
            await _oneDriveHelper.Sync(oneDriveInfo);
        }
    }
}

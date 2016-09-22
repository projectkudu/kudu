using System.Web;
using Newtonsoft.Json.Linq;
using Kudu.Core;
using System;
using Kudu.Core.Infrastructure;
using System.IO;

namespace Kudu.Services.ServiceHookHandlers
{
    public class GitHubHandler : GitHubCompatHandler
    {
        private IEnvironment environmentInject { get; set; } = null;

        private const string PrivateKeyFile = "id_rsa";
        // check for its presence to determine whether we use ssh or https

        public GitHubHandler()
        {
            // do nothing, 5 tests called this function
        }

        public GitHubHandler(IEnvironment enviroment)
        {
            environmentInject = enviroment;
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;
            if (request.Headers["X-Github-Event"] != null)
            {
                GitDeploymentInfo gitDeploymentInfo = GetDeploymentInfo(request, payload, targetBranch);
                deploymentInfo = gitDeploymentInfo;
                return deploymentInfo == null || IsDeleteCommit(gitDeploymentInfo.NewRef) ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }
            return DeployAction.UnknownPayload;
        }

        protected override string determineSecurityProtocol(JObject repository)
        {
            string repositoryUrl = repository.Value<string>("url");
            bool isPrivate = repository.Value<bool>("private");
            if (isPrivate)
            {
                repositoryUrl = repository.Value<string>("ssh_url");
            }
            else if (!String.Equals(new Uri(repositoryUrl).Host, "github.com"))
            {
                // github enterprise user, we don't really need to check for null...since only test will invoke githubhandler
                if (environmentInject != null && FileSystemHelpers.FileExists(Path.Combine(environmentInject.SSHKeyPath, PrivateKeyFile)))
                {
                    repositoryUrl = repository.Value<string>("ssh_url");
                }
            }
            return repositoryUrl;
        }

        protected override string GetDeployer(HttpRequestBase request)
        {
            return "GitHub";
        }
    }
}
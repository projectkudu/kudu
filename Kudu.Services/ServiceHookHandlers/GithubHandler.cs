using Kudu.Core;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Web;

namespace Kudu.Services.ServiceHookHandlers
{
    public class GitHubHandler : GitHubCompatHandler
    {
        private IEnvironment _environment;
        private const string PrivateKeyFile = "id_rsa";

        public GitHubHandler()
        {
            // do nothing, 5 tests called this function
            _environment = null;
        }

        public GitHubHandler(IEnvironment enviromentInject)
        {
            _environment = enviromentInject;
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

        protected override string DetermineSecurityProtocol(JObject repository)
        {
            string repositoryUrl = repository.Value<string>("url");
            bool isPrivate = repository.Value<bool>("private");
            if (isPrivate)
            {
                repositoryUrl = repository.Value<string>("ssh_url");
            }
            else if (!String.Equals(new Uri(repositoryUrl).Host, "github.com", StringComparison.OrdinalIgnoreCase))
            {
                
                if (_environment != null && FileSystemHelpers.FileExists(Path.Combine(_environment.SSHKeyPath, PrivateKeyFile)))
                {
                    // if we determine that a github request does not come from "github.com"
                    // and it is not a private repository, but we still find a ssh key, we assume 
                    // the request is from a private GHE 
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
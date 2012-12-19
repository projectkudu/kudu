using System.Web;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class CodePlexHandler : ServiceHookHandlerBase
    {
        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            // Look for the generic format
            // { url: "", branch: "", deployer: "", oldRef: "", newRef: "" } 
            deploymentInfo = new DeploymentInfo
            {
                RepositoryUrl = payload.Value<string>("url"),
                Deployer = payload.Value<string>("deployer"),
            };

            return deploymentInfo.IsValid() ? DeployAction.ProcessDeployment : DeployAction.UnknownPayload;
        }
    }
}
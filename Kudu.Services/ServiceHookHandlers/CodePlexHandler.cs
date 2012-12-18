using System.Web;
using Kudu.Contracts.SourceControl;
using Kudu.Core;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public class CodePlexHandler : PolymorphicHandler
    {
        public CodePlexHandler(IGitServer gitServer, ITraceFactory tracer, IEnvironment environment, RepositoryConfiguration repositoryConfiguration)
            : base(gitServer, tracer, environment, repositoryConfiguration)
        {

        }

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
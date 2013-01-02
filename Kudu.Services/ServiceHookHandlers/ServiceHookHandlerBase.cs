using Kudu.Core.SourceControl;

namespace Kudu.Services.ServiceHookHandlers
{
    public abstract class ServiceHookHandlerBase :  IServiceHookHandler
    {
        public abstract DeployAction TryParseDeploymentInfo(System.Web.HttpRequestBase request, Newtonsoft.Json.Linq.JObject payload, string targetBranch, out DeploymentInfo deploymentInfo);

        public void Fetch(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch)
        {
            repository.FetchWithoutConflict(deploymentInfo.RepositoryUrl, "external", targetBranch);
        }
    }
}

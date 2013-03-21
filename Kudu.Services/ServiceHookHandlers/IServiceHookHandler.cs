using System.Threading.Tasks;
using System.Web;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    public interface IServiceHookHandler
    {
        /// <param name="request">The incoming request.</param>
        /// <param name="payload">The parsed payload from the request.</param>
        /// <param name="targetBranch">The branch configured for deployment.</param>
        /// <param name="deploymentInfo">The parsed deployment info if successful and matches the target branch, null otherwise.</param>
        /// <returns>True if successfully parsed</returns>
        DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo);

        Task Fetch(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch, ILogger logger);
    }

    public enum DeployAction
    {
        UnknownPayload,
        NoOp,
        ProcessDeployment
    }

}
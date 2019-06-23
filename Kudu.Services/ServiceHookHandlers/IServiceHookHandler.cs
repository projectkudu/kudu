using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Tracing;
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
        DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfoBase deploymentInfo);

        Task Fetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer);
    }

    public enum DeployAction
    {
        UnknownPayload,
        NoOp,
        ProcessDeployment
    }

}
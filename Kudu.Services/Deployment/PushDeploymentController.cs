using System;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;
using System.IO;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;

namespace Kudu.Services.Deployment
{
    public class PushDeploymentController : ApiController
    {
        private readonly IFetchDeploymentManager _deploymentManager;
        private readonly ITracer _tracer;

        public PushDeploymentController(IFetchDeploymentManager deploymentManager, ITracer tracer)
        {
            _deploymentManager = deploymentManager;
            _tracer = tracer;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> ZipPushDeploy()
        {
            using (_tracer.Step("ZipPushDeploy"))
            {
                try
                {
                    // TODO do we need to acquire lock and create temp deployment during zip upload?
                    // If we do, need to signal to FetchDeploy that we have already done both.
                    // If we create a temp deployment it should be done inside the lock, see
                    // https://github.com/projectkudu/kudu/issues/2301

                    // TODO where to put the zip file?
                    var filepath = Path.GetTempFileName();

                    using (var file = File.OpenWrite(filepath))
                    {
                        await Request.Content.CopyToAsync(file);
                    }
                  
                    // TODO support async based on request
                    // TODO this should be part of DeploymentInfo, along with the other parameters as well, and it should
                    // be called FetchDeploymentInfo
                    var asyncRequested = false;

                    var deploymentInfo = new DeploymentInfo
                    {
                        AllowDeploymentWhileScmDisabled = true, // TODO ??
                        Deployer = "Zip-Push",
                        IsContinuous = false, // TODO check on this - prob. keep false. It *forces* background/async deployment, but also does another check in ShouldDeploy I'm not sure about
                        IsReusable = false, // TODO ??
                        RepositoryUrl = "", // TODO I dont't think this will actually be used in the implementation, but set it to the file:/// location maybe
                        TargetChangeset = null, // Needed for status file?
                        CommitId = null, // Don't think this is needed for anything
                        RepositoryType = RepositoryType.None, // TODO may need a new value here to trigger correct logic
                        Fetch = null // TODO need a delegate that unzips
                    };

                    var result = await _deploymentManager.FetchDeploy(deploymentInfo, asyncRequested, UriHelper.GetRequestUri(Request), "HEAD");

                    var response = Request.CreateResponse();

                    switch (result)
                    {
                        // TODO original implementation this is cribbed from is in FetchHandler. Need context.ApplicationInstance.CompleteRequest?
                        case FetchDeploymentRequestResult.RunningAynschronously:
                            // to avoid regression, only set location header if isAsync
                            if (asyncRequested)
                            {
                                // latest deployment keyword reserved to poll till deployment done
                                response.Headers.Location = new Uri(UriHelper.GetRequestUri(Request),
                                    String.Format("/api/deployments/{0}?deployer={1}&time={2}", Constants.LatestDeployment, deploymentInfo.Deployer, DateTime.UtcNow.ToString("yyy-MM-dd_HH-mm-ssZ")));
                            }
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                        case FetchDeploymentRequestResult.ForbiddenScmDisabled:
                            response.StatusCode = HttpStatusCode.Forbidden;
                            _tracer.Trace("Scm is not enabled, reject all requests.");
                            break;
                        case FetchDeploymentRequestResult.AutoSwapOngoing:
                            response.StatusCode = HttpStatusCode.Conflict;
                            response.Content = new StringContent(Resources.Error_AutoSwapDeploymentOngoing);
                            break;
                        case FetchDeploymentRequestResult.Pending:
                            // Return a http 202: the request has been accepted for processing, but the processing has not been completed.
                            response.StatusCode = HttpStatusCode.Accepted;
                            break;
                        case FetchDeploymentRequestResult.RanSynchronously:
                            response.StatusCode = HttpStatusCode.OK;
                            break;
                        default:
                            response.StatusCode = HttpStatusCode.BadRequest;
                            break;
                    }

                    return response;
                }
                catch (LockOperationException)
                {
                    // TODO need to handle marker files 
                    return Request.CreateResponse(HttpStatusCode.Conflict);
                }
            }
        }
    }
}

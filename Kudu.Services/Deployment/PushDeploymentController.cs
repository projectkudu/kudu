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
using static Kudu.Services.ServiceHookHandlers.DeploymentInfo;
using System.Globalization;

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
                // TODO do we need to acquire lock and create temp deployment during zip upload?
                // If we do, need to signal to FetchDeploy that we have already done one or noth.
                // Despite https://github.com/projectkudu/kudu/issues/2301, in this case we may
                // be OK creating a temporary deployment outside of the lock due to the way this API is used.

                // TODO where to put the zip file?
                var filepath = Path.GetTempFileName();

                using (var file = File.OpenWrite(filepath))
                {
                    await Request.Content.CopyToAsync(file);
                }

                // TODO support async based on request
                // TODO async indicator should be part of DeploymentInfo, along with the other parameters as well, and it should
                // be called FetchDeploymentInfo
                var asyncRequested = false;

                var deploymentInfo = new DeploymentInfo
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = "Zip-Push",
                    IsContinuous = false,
                    IsReusable = false, // TODO?? I think just exists to prevent the marker file loop from running. Should probably remain false.
                    RepositoryUrl = filepath, // TODO this is kind of an ugly overload of this. Maybe just bake filepath into the Fetch() closure?
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Deploying from zip"), // TODO better msg?
                    CommitId = null,
                    RepositoryType = RepositoryType.Prebuilt,
                    Fetch = LocalZipFetch,
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
        }

        private static async Task LocalZipFetch(IRepository repository, DeploymentInfo deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            // TODO update status file here?
            
            // For this deployment, RepositoryUrl is a local path.
            using (var file = File.OpenRead(deploymentInfo.RepositoryUrl))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
            {
                await Task.Run(() => zip.Extract(repository.RepositoryPath));
            }

            // Needed in order for repository.GetChangeSet() to work.
            // Similar to what OneDriveHelper and DropBoxHelper do.
            var message = String.Format(CultureInfo.InvariantCulture, "Unzipped {0} to {1}", deploymentInfo.RepositoryUrl, repository.RepositoryPath);
            repository.Commit(message, null, null);
        }
    }
}

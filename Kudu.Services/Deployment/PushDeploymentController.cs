using System;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;
using System.IO;
using Kudu.Core.SourceControl;
using System.Globalization;
using Kudu.Core;

namespace Kudu.Services.Deployment
{
    public class PushDeploymentController : ApiController
    {
        private readonly IEnvironment _environment;
        private readonly IFetchDeploymentManager _deploymentManager;
        private readonly ITracer _tracer;

        public PushDeploymentController(IEnvironment environment, IFetchDeploymentManager deploymentManager, ITracer tracer)
        {
            _environment = environment;
            _deploymentManager = deploymentManager;
            _tracer = tracer;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> ZipPushDeploy(HttpRequestMessage request, [FromUri] bool isAsync = false)
        {
            using (_tracer.Step("ZipPushDeploy"))
            { 
                var filepath = Path.Combine(_environment.TempPath, Path.GetRandomFileName());

                using (_tracer.Step("Writing zip file to {0}", filepath))
                {
                    using (var file = File.OpenWrite(filepath))
                    {
                        await request.Content.CopyToAsync(file);
                    }
                }

                var deploymentInfo = new DeploymentInfo
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = "Zip-Push",
                    IsContinuous = false,
                    AllowDeferral = false,
                    IsReusable = false,
                    RepositoryUrl = filepath,
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Deploying from pushed zip file"),
                    CommitId = null,
                    RepositoryType = RepositoryType.Zip,
                    Fetch = LocalZipFetch,
                };

                var result = await _deploymentManager.FetchDeploy(deploymentInfo, isAsync, UriHelper.GetRequestUri(Request), "HEAD");

                var response = request.CreateResponse();

                switch (result)
                {
                    case FetchDeploymentRequestResult.RunningAynschronously:
                        if (isAsync)
                        {
                            // latest deployment keyword reserved to poll till deployment done
                            response.Headers.Location = new Uri(UriHelper.GetRequestUri(Request),
                                String.Format("/api/deployments/{0}?deployer={1}&time={2}", Constants.LatestDeployment, deploymentInfo.Deployer, DateTime.UtcNow.ToString("yyy-MM-dd_HH-mm-ssZ")));
                        }
                        response.StatusCode = HttpStatusCode.Accepted;
                        break;
                    case FetchDeploymentRequestResult.ForbiddenScmDisabled:
                        // Should never hit this for zip push deploy
                        response.StatusCode = HttpStatusCode.Forbidden;
                        _tracer.Trace("Scm is not enabled, reject all requests.");
                        break;
                    case FetchDeploymentRequestResult.ConflictAutoSwapOngoing:
                        response.StatusCode = HttpStatusCode.Conflict;
                        response.Content = new StringContent(Resources.Error_AutoSwapDeploymentOngoing);
                        break;
                    case FetchDeploymentRequestResult.Pending:
                        // Shouldn't happen here, as we disallow deferral for this use case
                        response.StatusCode = HttpStatusCode.Accepted;
                        break;
                    case FetchDeploymentRequestResult.RanSynchronously:
                        response.StatusCode = HttpStatusCode.OK;
                        break;
                    case FetchDeploymentRequestResult.ConflictDeploymentInProgress:
                        response.StatusCode = HttpStatusCode.Conflict;
                        response.Content = new StringContent(Resources.Error_DeploymentInProgress);
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
            // For this deployment, RepositoryUrl is a local path.
            var sourceZipFile = deploymentInfo.RepositoryUrl;
            var extractTargetDirectory = repository.RepositoryPath;

            var info = FileSystemHelpers.FileInfoFromFileName(sourceZipFile);
            var sizeInMb = (info.Length / (1024f * 1024f)).ToString("0.00", CultureInfo.InvariantCulture);

            var message = String.Format(
                CultureInfo.InvariantCulture,
                "Extracting pushed zip file {0} ({1} MB) to {2}",
                info.FullName,
                sizeInMb,
                repository.RepositoryPath);

            logger.Log(message);

            using (tracer.Step(message))
            {
                FileSystemHelpers.CreateDirectory(extractTargetDirectory);

                using (var file = info.OpenRead())
                using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                {
                    await Task.Run(() => zip.Extract(extractTargetDirectory));
                }
            }

            // Needed in order for repository.GetChangeSet() to work.
            // Similar to what OneDriveHelper and DropBoxHelper do.
            repository.Commit("Created via zip push deployment", null, null);
        }
    }
}

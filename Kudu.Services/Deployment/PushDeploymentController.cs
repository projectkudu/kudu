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
using System.Linq;
using System.IO.Abstractions;

namespace Kudu.Services.Deployment
{
    public class PushDeploymentController : ApiController
    {
        private readonly IEnvironment _environment;
        private readonly IFetchDeploymentManager _deploymentManager;
        private readonly ITracer _tracer;
        private readonly ITraceFactory _traceFactory;

        public PushDeploymentController(
            IEnvironment environment,
            IFetchDeploymentManager deploymentManager,
            ITracer tracer,
            ITraceFactory traceFactory)
        {
            _environment = environment;
            _deploymentManager = deploymentManager;
            _tracer = tracer;
            _traceFactory = traceFactory;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> ZipPushDeploy(HttpRequestMessage request, [FromUri] bool isAsync = false)
        {
            using (_tracer.Step("ZipPushDeploy"))
            {
                var zipFileName = Path.ChangeExtension(Path.GetRandomFileName(), "zip");
                var zipFilePath = Path.Combine(_environment.ZipTempPath, zipFileName);

                using (_tracer.Step("Writing zip file to {0}", zipFilePath))
                {
                    using (var file = FileSystemHelpers.CreateFile(zipFilePath))
                    {
                        await request.Content.CopyToAsync(file);
                    }
                }

                var deploymentInfo = new ZipDeploymentInfo(_environment, _traceFactory)
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = "Zip-Push",
                    IsContinuous = false,
                    AllowDeferredDeployment = false,
                    IsReusable = false,
                    RepositoryUrl = zipFilePath,
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Deploying from pushed zip file"),
                    CommitId = null,
                    RepositoryType = RepositoryType.None,
                    Fetch = LocalZipFetch,
                    DoFullBuildByDefault = false
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

        private async Task LocalZipFetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            // For this kind of deployment, RepositoryUrl is a local path.
            var sourceZipFile = deploymentInfo.RepositoryUrl;
            var extractTargetDirectory = repository.RepositoryPath;

            var info = FileSystemHelpers.FileInfoFromFileName(sourceZipFile);
            var sizeInMb = (info.Length / (1024f * 1024f)).ToString("0.00", CultureInfo.InvariantCulture);

            var message = String.Format(
                CultureInfo.InvariantCulture,
                "Cleaning up temp folders from previous zip deployments and extracting pushed zip file {0} ({1} MB) to {2}",
                info.FullName,
                sizeInMb,
                extractTargetDirectory);

            logger.Log(message);

            using (tracer.Step(message))
            {
                // If extractTargetDirectory already exists, rename it so we can delete it concurrently with
                // the unzip (along with any other junk in the folder)
                var targetInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(extractTargetDirectory);
                if (targetInfo.Exists)
                {
                    var moveTarget = Path.Combine(targetInfo.Parent.FullName, Path.GetRandomFileName());
                    targetInfo.MoveTo(moveTarget);
                }

                var cleanTask = Task.Run(() => DeleteFilesAndDirsExcept(sourceZipFile, extractTargetDirectory, tracer));
                var extractTask = Task.Run(() =>
                {
                    FileSystemHelpers.CreateDirectory(extractTargetDirectory);

                    using (var file = info.OpenRead())
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        zip.Extract(extractTargetDirectory);
                    }
                });

                await Task.WhenAll(cleanTask, extractTask);
            }

            // Needed in order for repository.GetChangeSet() to work.
            // Similar to what OneDriveHelper and DropBoxHelper do.
            repository.Commit("Created via zip push deployment", null, null);
        }

        private void DeleteFilesAndDirsExcept(string fileToKeep, string dirToKeep, ITracer tracer)
        {
            // Best effort. Using the "Safe" variants does retries and swallows exceptions but
            // we may catch something non-obvious.
            try
            {
                var files = FileSystemHelpers.GetFiles(_environment.ZipTempPath, "*")
                .Where(p => !PathUtilityFactory.Instance.PathsEquals(p, fileToKeep));

                foreach (var file in files)
                {
                    FileSystemHelpers.DeleteFileSafe(file);
                }

                var dirs = FileSystemHelpers.GetDirectories(_environment.ZipTempPath)
                    .Where(p => !PathUtilityFactory.Instance.PathsEquals(p, dirToKeep));

                foreach (var dir in dirs)
                {
                    FileSystemHelpers.DeleteDirectorySafe(dir);
                }
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex, "Exception encountered during zip folder cleanup");
                throw;
            }
        }
    }
}

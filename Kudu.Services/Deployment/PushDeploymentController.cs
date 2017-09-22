using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;

using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Services.Deployment
{
    public class PushDeploymentController : ApiController
    {
        //private readonly IDeploymentStatusManager _status;
        private readonly IDeploymentManager _deploymentManager;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IEnvironment _environment;
        //private readonly IDeploymentSettingsManager _settings;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly string _markerFilePath;

        public PushDeploymentController(
            //IDeploymentStatusManager status,
            IDeploymentManager deploymentManager,
            ITracer tracer,
            IOperationLock deploymentLock,
            IEnvironment environment,
            //IDeploymentSettingsManager settings,
            IRepositoryFactory repositoryFactory)
        {
            //_status = status;
            _deploymentManager = deploymentManager;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _environment = environment;
            //_settings = settings;
            _repositoryFactory = repositoryFactory;
            _markerFilePath = Path.Combine(_environment.DeploymentsPath, "pending");

            // TODO the below is from fetch handler
            // This should be refactored to somewhere central

            // Prefer marker creation in ctor to delay create when needed.
            // This is to keep the code simple and avoid creation synchronization.
            if (!FileSystemHelpers.FileExists(_markerFilePath))
            {
                try
                {
                    FileSystemHelpers.WriteAllText(_markerFilePath, String.Empty);
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                }
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> ZipPushDeploy()
        {
            using (_tracer.Step("ZipPushDeploy"))
            {

                // TODO should we reject requests if SCM is enabled?
                // FetchHandler does on line 115, but explicitly calls out that that
                // check is skipped for git pushes and GenericHandler/DropBoxHandler because
                // they "can only be done by user action, we loosely allow that and
                // assume users know what they are doing"

                try
                {
                    return await _deploymentLock.LockOperationAsync(async () =>
                    {
                        if (PostDeploymentHelper.IsAutoSwapOngoing())
                        {
                            // TODO make sure this works properly. Copied from FetchHandler but that uses
                            // context
                            return Request.CreateErrorResponse(HttpStatusCode.Conflict, Resources.Error_AutoSwapDeploymentOngoing);
                        }

                        // TODO Create temp deployment here

                        // TODO What should the path be? 
                        // should repositoryFactory take care of it? What should it be based on?
                        // Should it vary between deployments?
                        // Should it be the repository folder (don't see the point of that, when we could put it
                        // in a temp folder on the local drive for speed)
                        // Should it always be a new folder? If not, do we clean it out first?
                        // Also make sure that it's on the local drive and not on the user drive for speed.
                        // size concerns?

                        // Ugly hack here for now
                        var extractPath = Path.Combine(_environment.TempPath, Path.GetRandomFileName());

                        // TODO: For repository, should we do an EnsureRepository check, or just new it up?
                        // Not sure if we want to fail with a repository mismatch error if other work already done?
                        // TODO note that Fetch *always* does EnsureRepository, so what's happening here is not compatible
                        // with that setup as-is.
                        var repository = _repositoryFactory.GetZipDeployRepository(extractPath);

                        // TODO For async deploys, write the stream to a file and extract later
                        using (var stream = await Request.Content.ReadAsStreamAsync())
                        {
                            var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                            zipArchive.Extract(extractPath);
                        }

                        // TODO Doing changeset this way for consistency with future fetch deploy implementation,
                        // which requires that the repository have changeset that can be retrieved with GetChangeSet.
                        // The below is what we do for OneDrive and Dropbox during a fetch.
                        // Not sure if it's the right thing to do.
                        repository.Commit("Extracting pushed zip file", authorName: null, emailAddress: null);
                        var changeSet = repository.GetChangeSet("HEAD");

                        var deployer = "Zip-Push";
                        await _deploymentManager.DeployAsync(repository, changeSet, deployer, clean: false, needFileUpdate: false);

                        return Request.CreateResponse(HttpStatusCode.OK);
                    }, "Performing zip push deployment", TimeSpan.Zero);
                }
                catch (LockOperationException)
                {
                    // TODO Not sure if any of this is desired, as currently not do/while looping
                    // over the markerfile in PerformDeployment.

                    // Create a marker file that indicates if there's another deployment to pull
                    // because there was a deployment in progress.
                    using (_tracer.Step("Update pending deployment marker file"))
                    {
                        // REVIEW: This makes the assumption that the repository url is the same.
                        // If it isn't the result would be buggy either way.
                        FileSystemHelpers.SetLastWriteTimeUtc(_markerFilePath, DateTime.UtcNow);
                    }

                    // Return a http 202: the request has been accepted for processing, but the processing has not been completed.
                    //context.Response.StatusCode = (int)HttpStatusCode.Accepted;
                    return Request.CreateResponse(HttpStatusCode.Accepted);
                }
            }
        }
    }
}

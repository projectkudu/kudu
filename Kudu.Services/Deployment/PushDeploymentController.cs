using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.ServiceHookHandlers;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Kudu.Services.Deployment
{
    public class PushDeploymentController : ApiController
    {
        private readonly IDeploymentStatusManager _status;
        private readonly IDeploymentManager _deploymentManager;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly string _markerFilePath;

        public PushDeploymentController(
            IDeploymentStatusManager status,
            IDeploymentManager deploymentManager,
            ITracer tracer,
            IOperationLock deploymentLock,
            IEnvironment environment,
            IDeploymentSettingsManager settings,
            IRepositoryFactory repositoryFactory)
        {
            _status = status;
            _deploymentManager = deploymentManager;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _environment = environment;
            _settings = settings;
            _repositoryFactory = repositoryFactory;
            _markerFilePath = Path.Combine(environment.DeploymentsPath, "pending");

            // TODO this is from fetch handler
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
                // (like FetchHandler line 115)

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

                        // TODO create temp deployment here?

                        // TODO: For repository, should we do an EnsureRepository check, or just new it up?
                        // Not sure if we want to fail with a repository mismatch error if other work already done?
                        var repository = _repositoryFactory.EnsureRepository(RepositoryType.None);

                        // TODO Not sure if inside the lock is the right place to extract the stream.
                        // TODO Would we rather write it to disk first? Should probably always do that 
                        // for async deploys.

                        using (var stream = await Request.Content.ReadAsStreamAsync())
                        {
                            // TODO Need to clear out any files already in the repository folder
                            // until we implement a handler that does delta-based extraction
                            // including deletion of files in the repository folder that are no longer there
                            var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);

                            // TODO correct path
                            zipArchive.Extract("/home/site/wwwroot");
                        }

                        var deployer = "Zip-Push";

                        // TODO We don't need to bother constructing a DeploymentInfo here but
                        // saving this for reference when doing fetch later
                        //var deployInfo = new DeploymentInfo();
                        //deployInfo.RepositoryType = RepositoryType.None;
                        //deployInfo.Deployer = "Zip-Push";
                        //deployInfo.TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(
                        //    message: "Deploying from zip file");

                        // TODO Doing changeset this way for consistency with future fetch deploy implementation,
                        // which requires that the repository have changeset that can be retrieved with GetChangeSet.
                        // The beloq is what we do for OneDrive and Dropbox during a fetch.
                        // Not sure if it's the right thing to do.
                        repository.Commit("Extracting pushed zip file", authorName: null, emailAddress: null);
                        var changeSet = repository.GetChangeSet("HEAD");

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

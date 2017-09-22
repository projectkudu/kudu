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
                        // Also do it for async. Make sure to do it inside the lock!
                        // See https://github.com/projectkudu/kudu/issues/2301

                        var repository = _repositoryFactory.GetZipDeployRepository();

                        // TODO status file for tracking unzip progress.
                        // See OneDrive and BitBucket handlers for how they do Sync.

                        // TODO For async deploys, write the stream to a file and extract later.
                        // Any reason to do that for sync deploys as well? Maybe just consistency?
                        using (var stream = await Request.Content.ReadAsStreamAsync())
                        {
                            var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);
                            zipArchive.Extract(repository.RepositoryPath);
                        }

                        // This is the standard interaction for a NullRepository.
                        // It's what we do for OneDrive and Dropbox during fetches for those providers.
                        repository.Commit("Extracting pushed zip file", authorName: null, emailAddress: null);
                        var changeSet = repository.GetChangeSet("HEAD");

                        var deployer = "Zip-Push";
                        await _deploymentManager.DeployAsync(repository, changeSet, deployer, clean: false, needFileUpdate: false);

                        return Request.CreateResponse(HttpStatusCode.OK);
                    }, "Performing zip push deployment", TimeSpan.Zero);
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

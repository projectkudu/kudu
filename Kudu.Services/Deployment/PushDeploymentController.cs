using System;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.Deployment
{
    public class PushDeploymentController : ApiController
    {
        private const string DefaultDeployer = "Push-Deployer";
        private const string DefaultMessage = "Created via a push deployment";

        private readonly IEnvironment _environment;
        private readonly IFetchDeploymentManager _deploymentManager;
        private readonly ITracer _tracer;
        private readonly ITraceFactory _traceFactory;
        private readonly IDeploymentSettingsManager _settings;

        public PushDeploymentController(
            IEnvironment environment,
            IFetchDeploymentManager deploymentManager,
            ITracer tracer,
            ITraceFactory traceFactory,
            IDeploymentSettingsManager settings)
        {
            _environment = environment;
            _deploymentManager = deploymentManager;
            _tracer = tracer;
            _traceFactory = traceFactory;
            _settings = settings;
        }

        [HttpPost]
        public async Task<HttpResponseMessage> ZipPushDeploy(
            [FromUri] bool isAsync = false,
            [FromUri] string author = null,
            [FromUri] string authorEmail = null,
            [FromUri] string deployer = DefaultDeployer,
            [FromUri] string message = DefaultMessage)
        {
            using (_tracer.Step("ZipPushDeploy"))
            {
                var deploymentInfo = new ZipDeploymentInfo(_environment, _traceFactory)
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = deployer,
                    IsContinuous = false,
                    AllowDeferredDeployment = false,
                    IsReusable = false,
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Deploying from pushed zip file"),
                    CommitId = null,
                    RepositoryType = RepositoryType.None,
                    Fetch = LocalZipHandler,
                    DoFullBuildByDefault = false,
                    Author = author,
                    AuthorEmail = authorEmail,
                    Message = message
                };

                if (_settings.RunFromLocalZip())
                {
                    // This is used if the deployment is Run-From-Zip
                    // the name of the deployed file in D:\home\data\SitePackages\{name}.zip is the 
                    // timestamp in the format yyyMMddHHmmss. 
                    deploymentInfo.ZipName = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.zip";

                    // This is also for Run-From-Zip where we need to extract the triggers
                    // for post deployment sync triggers.
                    deploymentInfo.SyncFunctionsTriggersPath = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
                }

                return await PushDeployAsync(deploymentInfo, isAsync);
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> WarPushDeploy(
            [FromUri] bool isAsync = false,
            [FromUri] string author = null,
            [FromUri] string authorEmail = null,
            [FromUri] string deployer = DefaultDeployer,
            [FromUri] string message = DefaultMessage)
        {
            using (_tracer.Step("WarPushDeploy"))
            {
                var appName = Request.RequestUri.ParseQueryString()["name"];

                if (string.IsNullOrWhiteSpace(appName))
                {
                    appName = "ROOT";
                }

                var deploymentInfo = new ZipDeploymentInfo(_environment, _traceFactory)
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = deployer,
                    TargetPath = Path.Combine("webapps", appName),
                    WatchedFilePath = Path.Combine("WEB-INF", "web.xml"),
                    IsContinuous = false,
                    AllowDeferredDeployment = false,
                    IsReusable = false,
                    CleanupTargetDirectory = true, // For now, always cleanup the target directory. If needed, make it configurable
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Deploying from pushed war file"),
                    CommitId = null,
                    RepositoryType = RepositoryType.None,
                    Fetch = LocalZipFetch,
                    DoFullBuildByDefault = false,
                    Author = author,
                    AuthorEmail = authorEmail,
                    Message = message
                };

                return await PushDeployAsync(deploymentInfo, isAsync);
            }
        }

        private async Task<HttpResponseMessage> PushDeployAsync(ZipDeploymentInfo deploymentInfo, bool isAsync)
        {
            if (_settings.RunFromLocalZip())
            {
                await WriteSitePackageZip(deploymentInfo, _tracer);
            }
            else
            {
                var zipFileName = Path.ChangeExtension(Path.GetRandomFileName(), "zip");
                var zipFilePath = Path.Combine(_environment.ZipTempPath, zipFileName);

                using (_tracer.Step("Writing content to {0}", zipFilePath))
                {
                    using (var file = FileSystemHelpers.CreateFile(zipFilePath))
                    {
                        await Request.Content.CopyToAsync(file);
                    }
                }

                deploymentInfo.RepositoryUrl = zipFilePath;
            }

            var result = await _deploymentManager.FetchDeploy(deploymentInfo, isAsync, UriHelper.GetRequestUri(Request), "HEAD");

            var response = Request.CreateResponse();

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
                case FetchDeploymentRequestResult.ConflictRunFromRemoteZipConfigured:
                    response.StatusCode = HttpStatusCode.Conflict;
                    response.Content = new StringContent(Resources.Error_RunFromRemoteZipConfigured);
                    break;
                default:
                    response.StatusCode = HttpStatusCode.BadRequest;
                    break;
            }

            return response;
        }

        private async Task LocalZipHandler(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            if (_settings.RunFromLocalZip() && deploymentInfo is ZipDeploymentInfo)
            {
                // If this is a Run-From-Zip deployment, then we need to extract function.json
                // from the zip file into path zipDeploymentInfo.SyncFunctionsTrigersPath
                ExtractTriggers(repository, deploymentInfo as ZipDeploymentInfo);
            }
            else
            {
                await LocalZipFetch(repository, deploymentInfo, targetBranch, logger, tracer);
            }
        }

        private void ExtractTriggers(IRepository repository, ZipDeploymentInfo zipDeploymentInfo)
        {
            FileSystemHelpers.EnsureDirectory(zipDeploymentInfo.SyncFunctionsTriggersPath);
            // Loading the zip file depends on how fast the file system is.
            // Tested Azure Files share with a zip containing 120k files (160 MBs)
            // takes 20 seconds to load. On my machine it takes 900 msec.
            using (var zip = ZipFile.OpenRead(Path.Combine(_environment.SitePackagesPath, zipDeploymentInfo.ZipName)))
            {
                var entries = zip.Entries
                    // Only select host.json, proxies.json, or top level directories.
                    // Tested with a zip containing 120k files, and this took 90 msec
                    // on my machine.
                    .Where(e => e.FullName.Equals(Constants.FunctionsHostConfigFile, StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.Equals(Constants.ProxyConfigFile, StringComparison.OrdinalIgnoreCase) ||
                                isTopLevelDirectory(e.FullName))
                    // If entry is a top level dir, select the function.json in it.
                    // otherwise that must be host.json, or proxies.json. Leave it as is.
                    .Select(e => isTopLevelDirectory(e.FullName)
                        ? zip.GetEntry($"{e.FullName}{Constants.FunctionsConfigFile}")
                        : e
                    )
                    // if a top level folder wasn't a function, it won't contain a function.json
                    // and GetEntry above will return null
                    .Where(e => e != null);

                foreach (var entry in entries)
                {
                    var path = Path.Combine(zipDeploymentInfo.SyncFunctionsTriggersPath, entry.FullName);
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(path));
                    entry.ExtractToFile(path, overwrite: true);
                }
            }

            CommitRepo(repository, zipDeploymentInfo);

            bool isTopLevelDirectory(string fullName)
            {
                for (var i = 0; i < fullName.Length; i++)
                {
                    // Zip entries use '/' separators.
                    // If the first '/' we find also the last in the string
                    // It's a top level dir, otherwise it's not
                    if (fullName[i] == '/')
                    {
                        return i + 1 == fullName.Length;
                    }
                }
                // Top level file
                return false;
            }
        }

        private async Task LocalZipFetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            var zipDeploymentInfo = (ZipDeploymentInfo)deploymentInfo;

            // For this kind of deployment, RepositoryUrl is a local path.
            var sourceZipFile = zipDeploymentInfo.RepositoryUrl;
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

            CommitRepo(repository, zipDeploymentInfo);
        }

        private static void CommitRepo(IRepository repository, ZipDeploymentInfo zipDeploymentInfo)
        {
            // Needed in order for repository.GetChangeSet() to work.
            // Similar to what OneDriveHelper and DropBoxHelper do.
            // We need to make to call respository.Commit() since deployment flow expects at
            // least 1 commit in the IRepository. Even though there is no repo per se in this
            // scenario, deployment pipeline still generates a NullRepository
            repository.Commit(zipDeploymentInfo.Message, zipDeploymentInfo.Author, zipDeploymentInfo.AuthorEmail);
        }

        private async Task WriteSitePackageZip(ZipDeploymentInfo zipDeploymentInfo, ITracer tracer)
        {
            var filePath = Path.Combine(_environment.SitePackagesPath, zipDeploymentInfo.ZipName);

            // Make sure D:\home\data\SitePackages exists
            FileSystemHelpers.EnsureDirectory(_environment.SitePackagesPath);

            using (tracer.Step("Writing content to {0}", filePath))
            {
                using (var file = FileSystemHelpers.CreateFile(filePath))
                {
                    await Request.Content.CopyToAsync(file);
                }
            }
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

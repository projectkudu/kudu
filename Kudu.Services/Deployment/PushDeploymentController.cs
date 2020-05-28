using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.ByteRanges;
using Kudu.Services.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Deployment
{
    public class PushDeploymentController : ApiController
    {
        private const string ZipDeploy = "ZipDeploy";
        private const string WarDeploy = "WarDeploy";
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
        [HttpPut]
        public async Task<HttpResponseMessage> ZipPushDeploy(
            [FromUri] bool isAsync = false,
            [FromUri] string author = null,
            [FromUri] string authorEmail = null,
            [FromUri] string deployer = ZipDeploy,
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
            [FromUri] string deployer = WarDeploy,
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

        private string GetZipURLFromARMJSON(JObject requestObject)
        {
            using (_tracer.Step("Reading the zip URL from the request JSON"))
            {
                try
                {
                    // ARM template should have properties field and a packageUri field inside the properties field.
                    string packageUri = requestObject.Value<JObject>("properties").Value<string>("packageUri");
                    if (string.IsNullOrEmpty(packageUri))
                    {
                        throw new ArgumentException("Invalid Url in the JSON request");
                    }
                    return packageUri;
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex, "Error reading the URL from the JSON {0}", requestObject.ToString());
                    throw;
                }
            }
        }

        private string GetZipURLFromJSON(JObject requestObject)
        {
            using (_tracer.Step("Reading the zip URL from the request JSON"))
            {
                try
                {
                    string packageUri = requestObject.Value<string>("packageUri");
                    if (string.IsNullOrEmpty(packageUri))
                    {
                        throw new ArgumentException("Invalid Url in the JSON request");
                    }
                    return packageUri;
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex, "Error reading the URL from the JSON {0}", requestObject.ToString());
                    throw;
                }
            }
        }

        private async Task<HttpResponseMessage> PushDeployAsync(ZipDeploymentInfo deploymentInfo, bool isAsync)
        {
            var content = Request.Content;
            var isRequestJSON = content.Headers?.ContentType?.MediaType?.Equals("application/json", StringComparison.OrdinalIgnoreCase);
            if (isRequestJSON == true)
            {
                try
                {
                    var requestObject = await Request.Content.ReadAsAsync<JObject>();
                    deploymentInfo.ZipURL = ArmUtils.IsArmRequest(Request) ? GetZipURLFromARMJSON(requestObject) : GetZipURLFromJSON(requestObject);
                }
                catch (Exception ex)
                {
                    return ArmUtils.CreateErrorResponse(Request, HttpStatusCode.BadRequest, ex);
                }
            }
            else
            {
                if (_settings.RunFromLocalZip())
                {
                    await WriteSitePackageZip(deploymentInfo, _tracer, Request.Content);
                }
                else
                {
                    var zipFileName = Path.ChangeExtension(Path.GetRandomFileName(), "zip");
                    var zipFilePath = Path.Combine(_environment.ZipTempPath, zipFileName);

                    using (_tracer.Step("Saving request content to {0}", zipFilePath))
                    {
                        await content.CopyToAsync(zipFilePath, _tracer);
                    }

                    deploymentInfo.RepositoryUrl = zipFilePath;
                }
            }

            isAsync = ArmUtils.IsArmRequest(Request) ? true : isAsync;

            var result = await _deploymentManager.FetchDeploy(deploymentInfo, isAsync, UriHelper.GetRequestUri(Request), "HEAD");

            var response = Request.CreateResponse();

            switch (result)
            {
                case FetchDeploymentRequestResult.RunningAynschronously:
                    if (ArmUtils.IsArmRequest(Request))
                    {
                        DeployResult deployResult = new DeployResult();
                        response = Request.CreateResponse(HttpStatusCode.Accepted, ArmUtils.AddEnvelopeOnArmRequest(deployResult, Request));
                        string statusURL = GetStatusUrl(Request.Headers.Referrer ?? Request.RequestUri);
                        // Should not happen: If we couldn't make the URL, there must have been an error in the request
                        if (string.IsNullOrEmpty(statusURL))
                        {
                            var badResponse = Request.CreateResponse();
                            badResponse.StatusCode = HttpStatusCode.BadRequest;
                            return badResponse;
                        }
                        // latest deployment keyword reserved to poll till deployment done
                        response.Headers.Location = new Uri(statusURL +
                            String.Format("/deployments/{0}?api-version=2018-02-01&deployer={1}&time={2}", Constants.LatestDeployment, deploymentInfo.Deployer, DateTime.UtcNow.ToString("yyy-MM-dd_HH-mm-ssZ")));
                    }
                    else if (isAsync)
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

        public static string GetStatusUrl(Uri uri)
        {
            var armID = uri.AbsoluteUri;
            string[] idTokens = armID.Split('/');
            // The Absolute URI looks like https://management.azure.com/subscriptions/<sub-id>/resourcegroups/<rg-name>/providers/Microsoft.Web/sites/<site-name>/extensions/zipdeploy?api-version=2016-03-01
            // or https://management.azure.com/subscriptions/<sub-id>/resourcegroups/<rg-name>/providers/Microsoft.Web/sites/<site-name>/slots/<slot-name>/extensions/zipdeploy?api-version=2016-03-01
            // We need everything up until <site-name>, without the endpoint.
            if (idTokens.Length > 10 && string.Equals(idTokens[8], "Microsoft.Web", StringComparison.OrdinalIgnoreCase))
            {
                if (idTokens.Length > 12 && string.Equals(idTokens[11], "slots", StringComparison.OrdinalIgnoreCase))
                {
                    return string.Join("/", idTokens.Take(13));
                }

                return string.Join("/", idTokens.Take(11));
            }
            return null;
        }

        private async Task LocalZipHandler(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            if (_settings.RunFromLocalZip() && deploymentInfo is ZipDeploymentInfo)
            {
                ZipDeploymentInfo zipDeploymentInfo = (ZipDeploymentInfo) deploymentInfo;
                // If this was a request with a Zip URL in the JSON, we first need to get the zip content and write it to the site.
                if (!string.IsNullOrEmpty(zipDeploymentInfo.ZipURL))
                {
                    await WriteSitePackageZip(zipDeploymentInfo, tracer, await DeploymentHelper.GetZipContentFromURL(zipDeploymentInfo, tracer));
                }
                // If this is a Run-From-Zip deployment, then we need to extract function.json
                // from the zip file into path zipDeploymentInfo.SyncFunctionsTriggersPath
                ExtractTriggers(repository, zipDeploymentInfo);
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
                    // Only select host.json, proxies.json, or function.json that are from top level directories only
                    // Tested with a zip containing 120k files, and this took 90 msec
                    // on my machine.
                    .Where(e => e.FullName.Equals(Constants.FunctionsHostConfigFile, StringComparison.OrdinalIgnoreCase) ||
                                e.FullName.Equals(Constants.ProxyConfigFile, StringComparison.OrdinalIgnoreCase) ||
                                isFunctionJson(e.FullName));

                foreach (var entry in entries)
                {
                    var path = Path.Combine(zipDeploymentInfo.SyncFunctionsTriggersPath, entry.FullName);
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(path));
                    entry.ExtractToFile(path, overwrite: true);
                }
            }

            CommitRepo(repository, zipDeploymentInfo);

            bool isFunctionJson(string fullName)
            {
                return fullName.EndsWith(Constants.FunctionsConfigFile) &&
                    fullName.Count(c => c == '/' || c == '\\') == 1;
            }
        }

        private async Task<string> DeployZipLocally(ZipDeploymentInfo zipDeploymentInfo, ITracer tracer)
        {
            var content = await DeploymentHelper.GetZipContentFromURL(zipDeploymentInfo, tracer);
            var zipFileName = Path.ChangeExtension(Path.GetRandomFileName(), "zip");
            var zipFilePath = Path.Combine(_environment.ZipTempPath, zipFileName);

            using (_tracer.Step("Downloading content from {0} to {1}", zipDeploymentInfo.ZipURL.Split('?')[0], zipFilePath))
            {
                await content.CopyToAsync(zipFilePath, _tracer);
            }

            zipDeploymentInfo.RepositoryUrl = zipFilePath;
            return zipFilePath;
        }

        private async Task LocalZipFetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            var zipDeploymentInfo = (ZipDeploymentInfo) deploymentInfo;

            // If this was a request with a Zip URL in the JSON, we need to deploy the zip locally and get the path
            // Otherwise, for this kind of deployment, RepositoryUrl is a local path.
            var sourceZipFile = !string.IsNullOrEmpty(zipDeploymentInfo.ZipURL)
                ? await DeployZipLocally(zipDeploymentInfo, tracer)
                : zipDeploymentInfo.RepositoryUrl;

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
                    using (tracer.Step(string.Format("Renaming extractTargetDirectory({0}) to tempDirectory({1})", targetInfo.FullName, moveTarget)))
                    {
                        targetInfo.MoveTo(moveTarget);
                    }
                }

                var cleanTask = Task.Run(() => DeleteFilesAndDirsExcept(sourceZipFile, extractTargetDirectory, tracer));
                var extractTask = Task.Run(() =>
                {
                    FileSystemHelpers.CreateDirectory(extractTargetDirectory);

                    using (var file = info.OpenRead())
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        zip.Extract(extractTargetDirectory, tracer, _settings.GetZipDeployDoNotPreserveFileTime());
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
            // We need to make to call repository.Commit() since deployment flow expects at
            // least 1 commit in the IRepository. Even though there is no repo per se in this
            // scenario, deployment pipeline still generates a NullRepository
            repository.Commit(zipDeploymentInfo.Message, zipDeploymentInfo.Author, zipDeploymentInfo.AuthorEmail);
        }

        private async Task WriteSitePackageZip(ZipDeploymentInfo zipDeploymentInfo, ITracer tracer, HttpContent content)
        {
            var filePath = Path.Combine(_environment.SitePackagesPath, zipDeploymentInfo.ZipName);

            // Make sure D:\home\data\SitePackages exists
            FileSystemHelpers.EnsureDirectory(_environment.SitePackagesPath);

            using (tracer.Step("Saving request content to {0}", filePath))
            {
                await content.CopyToAsync(filePath, tracer);
            }

            DeploymentHelper.PurgeZipsIfNecessary(_environment.SitePackagesPath, tracer, _settings.GetMaxZipPackageCount());
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

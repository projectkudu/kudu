using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using Kudu.Contracts.Deployment;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
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
        private readonly IOperationLock _deploymentLock;

        public PushDeploymentController(
            IEnvironment environment,
            IFetchDeploymentManager deploymentManager,
            ITracer tracer,
            ITraceFactory traceFactory,
            IDeploymentSettingsManager settings,
            IOperationLock deploymentLock)
        {
            _environment = environment;
            _deploymentManager = deploymentManager;
            _tracer = tracer;
            _traceFactory = traceFactory;
            _settings = settings;
            _deploymentLock = deploymentLock;
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
                var deploymentInfo = new ArtifactDeploymentInfo(_environment, _traceFactory)
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = deployer,
                    IsContinuous = false,
                    AllowDeferredDeployment = false,
                    IsReusable = false,
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Deploying from pushed zip file"),
                    CommitId = null,
                    DeploymentTrackingId = Guid.NewGuid().ToString(),
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
                    deploymentInfo.ArtifactFileName = $"{DateTime.UtcNow.ToString("yyyyMMddHHmmss")}.zip";

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

                var deploymentInfo = new ArtifactDeploymentInfo(_environment, _traceFactory)
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = deployer,
                    TargetSubDirectoryRelativePath = Path.Combine("webapps", appName),
                    WatchedFilePath = Path.Combine("WEB-INF", "web.xml"),
                    IsContinuous = false,
                    AllowDeferredDeployment = false,
                    IsReusable = false,
                    CleanupTargetDirectory = true, // For now, always cleanup the target directory. If needed, make it configurable
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: "Deploying from pushed war file"),
                    CommitId = null,
                    DeploymentTrackingId = Guid.NewGuid().ToString(),
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

        //
        // Supports:
        // 1. Deploy artifact in the request body:
        //    - For this: Query parameters should contain configuration.
        //                Example: /api/publish?type=war
        //                Request body should contain the artifact being deployed
        // 2. URL based deployment:
        //    - For this: Query parameters should contain configuration. Example: /api/publish?type=war
        //                Example: /api/publish?type=war
        //                Request body should contain JSON with 'packageUri' property pointing to the artifact location
        //                Example: { "packageUri": "http://foo/bar.war?accessToken=123" }
        // 3. ARM template based deployment:
        //    - For this: Query parameters are not supported.
        //                Request body should contain JSON with configuration as well as the artifact location
        //                Example: { "properties": { "type": "war", "packageUri": "http://foo/bar.war?accessToken=123" } }
        //
        [HttpPost]
        [HttpPut]
        public async Task<HttpResponseMessage> OneDeploy(
            [FromUri] string type = null,
            [FromUri] bool async = false,
            [FromUri] string path = null,
            [FromUri] bool? restart = true,
            [FromUri] bool? clean = null,
            [FromUri] bool ignoreStack = false
            )
        {
            using (_tracer.Step(Constants.OneDeploy))
            {
                JObject requestObject = null;

                try
                {
                    if (ArmUtils.IsArmRequest(Request))
                    {
                        requestObject = await Request.Content.ReadAsAsync<JObject>();
                        var armProperties = requestObject.Value<JObject>("properties");

                        type = armProperties.Value<string>("type");
                        async = armProperties.Value<bool>("async");
                        path = armProperties.Value<string>("path");
                        restart = armProperties.Value<bool?>("restart");
                        clean = armProperties.Value<bool?>("clean");
                        ignoreStack = armProperties.Value<bool>("ignorestack");
                    }
                }
                catch (Exception ex)
                {
                    return ArmUtils.CreateErrorResponse(Request, HttpStatusCode.BadRequest, ex);
                }

                //
                // 'async' is not a CSharp-ish variable name. And although it is a valid variable name, some
                // IDEs confuse it to be the 'async' keyword in C#.
                // On the other hand, isAsync is not a good name for the query-parameter.
                // So we use 'async' as the query parameter, and then assign it to the C# variable 'isAsync'
                // at the earliest. Hereon, we use just 'isAsync'.
                //
                bool isAsync = async;

                ArtifactType artifactType = ArtifactType.Unknown;
                try
                {
                    artifactType = (ArtifactType)Enum.Parse(typeof(ArtifactType), type, ignoreCase: true);
                }
                catch
                {
                    return StatusCode400($"type='{type}' not recognized");
                }

                var deploymentInfo = new ArtifactDeploymentInfo(_environment, _traceFactory)
                {
                    AllowDeploymentWhileScmDisabled = true,
                    Deployer = Constants.OneDeploy,
                    IsContinuous = false,
                    AllowDeferredDeployment = false,
                    IsReusable = false,
                    TargetRootPath = _environment.WebRootPath,
                    TargetChangeset = DeploymentManager.CreateTemporaryChangeSet(message: Constants.OneDeploy),
                    CommitId = null,
                    DeploymentTrackingId = Guid.NewGuid().ToString(),
                    RepositoryType = RepositoryType.None,
                    Fetch = OneDeployFetch,
                    DoFullBuildByDefault = false,
                    Message = Constants.OneDeploy,
                    WatchedFileEnabled = false,
                    CleanupTargetDirectory = clean.GetValueOrDefault(false),
                    RestartAllowed = restart.GetValueOrDefault(true),
                };

                string error;
                switch (artifactType)
                {
                    case ArtifactType.War:
                        if (!OneDeployHelper.EnsureValidStack(artifactType, new List<string> { OneDeployHelper.Tomcat, OneDeployHelper.JBossEap }, ignoreStack, out error))
                        {
                            return StatusCode400(error);
                        }

                        // If path is non-null, we assume this is a legacy war deployment, i.e. equivalent of wardeploy
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            //
                            // For legacy war deployments, the only path allowed is webapps/<directory-name>
                            //

                            if (!OneDeployHelper.EnsureValidPath(artifactType, OneDeployHelper.WwwrootDirectoryRelativePath, ref path, out error))
                            {
                                return StatusCode400(error);
                            }

                            if (!OneDeployHelper.IsLegacyWarPathValid(path))
                            {
                                return StatusCode400($"path='{path}' is invalid. When type={artifactType}, the only allowed paths are webapps/<directory-name> or /home/site/wwwroot/webapps/<directory-name>. " +
                                                     $"Example: path=webapps/ROOT or path=/home/site/wwwroot/webapps/ROOT");
                            }

                            deploymentInfo.TargetRootPath = Path.Combine(_environment.WebRootPath, path);
                            deploymentInfo.Fetch = LocalZipHandler;

                            // Legacy war deployment is equivalent to wardeploy
                            // So always do clean deploy.
                            deploymentInfo.CleanupTargetDirectory = true;
                            artifactType = ArtifactType.Zip;
                        }
                        else
                        {
                            // For type=war, if no path is specified, the target file is app.war
                            deploymentInfo.TargetFileName = "app.war";
                        }

                        break;

                    case ArtifactType.Jar:
                        if (!OneDeployHelper.EnsureValidStack(artifactType, new List<string> { OneDeployHelper.JavaSE }, ignoreStack, out error))
                        {
                            return StatusCode400(error);
                        }

                        deploymentInfo.TargetFileName = "app.jar";
                        break;

                    case ArtifactType.Ear:
                        if (!OneDeployHelper.EnsureValidStack(artifactType, new List<string> { OneDeployHelper.JBossEap }, ignoreStack, out error))
                        {
                            return StatusCode400(error);
                        }

                        deploymentInfo.TargetFileName = "app.ear";
                        break;

                    case ArtifactType.Lib:
                        if (!OneDeployHelper.EnsureValidPath(artifactType, OneDeployHelper.LibsDirectoryRelativePath, ref path, out error))
                        {
                            return StatusCode400(error);
                        }

                        deploymentInfo.TargetRootPath = OneDeployHelper.GetAbsolutePath(_environment, OneDeployHelper.LibsDirectoryRelativePath);
                        OneDeployHelper.SetTargetSubDirectoyAndFileNameFromRelativePath(deploymentInfo, path);
                        break;

                    case ArtifactType.Startup:
                        deploymentInfo.TargetRootPath = OneDeployHelper.GetAbsolutePath(_environment, OneDeployHelper.ScriptsDirectoryRelativePath);
                        OneDeployHelper.SetTargetSubDirectoyAndFileNameFromRelativePath(deploymentInfo, OneDeployHelper.GetStartupFileName());
                        break;

                    case ArtifactType.Script:
                        if (!OneDeployHelper.EnsureValidPath(artifactType, OneDeployHelper.ScriptsDirectoryRelativePath, ref path, out error))
                        {
                            return StatusCode400(error);
                        }

                        deploymentInfo.TargetRootPath = OneDeployHelper.GetAbsolutePath(_environment, OneDeployHelper.ScriptsDirectoryRelativePath);
                        OneDeployHelper.SetTargetSubDirectoyAndFileNameFromRelativePath(deploymentInfo, path);

                        break;

                    case ArtifactType.Static:
                        if (!OneDeployHelper.EnsureValidPath(artifactType, OneDeployHelper.WwwrootDirectoryRelativePath, ref path, out error))
                        {
                            return StatusCode400(error);
                        }

                        OneDeployHelper.SetTargetSubDirectoyAndFileNameFromRelativePath(deploymentInfo, path);

                        break;

                    case ArtifactType.Zip:
                        deploymentInfo.Fetch = LocalZipHandler;
                        deploymentInfo.TargetSubDirectoryRelativePath = path;

                        // Deployments for type=zip default to clean=true
                        deploymentInfo.CleanupTargetDirectory = clean.GetValueOrDefault(true);

                        break;

                    default:
                        return StatusCode400($"Artifact type '{artifactType}' not supported");
                }

                return await PushDeployAsync(deploymentInfo, isAsync, requestObject, artifactType);
            }
        }

        private HttpResponseMessage StatusCode400(string message)
        {
            return Request.CreateResponse(HttpStatusCode.BadRequest, message);
        }

        private string GetArticfactURLFromARMJSON(JObject requestObject)
        {
            using (_tracer.Step("Reading the artifact URL from the request JSON"))
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

        private string GetArtifactURLFromJSON(JObject requestObject)
        {
            using (_tracer.Step("Reading the artifact URL from the request JSON"))
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

        private async Task<HttpResponseMessage> PushDeployAsync(ArtifactDeploymentInfo deploymentInfo, bool isAsync, JObject requestObject = null, ArtifactType artifactType = ArtifactType.Zip)
        {
            var content = Request.Content;
            var isRequestJSON = content.Headers?.ContentType?.MediaType?.Equals("application/json", StringComparison.OrdinalIgnoreCase);
            if (isRequestJSON == true)
            {
                try
                {
                    // Read the request body if it hasn't been read already
                    if (requestObject == null)
                    {
                        requestObject = await Request.Content.ReadAsAsync<JObject>();
                    }
                    deploymentInfo.RemoteURL = ArmUtils.IsArmRequest(Request) ? GetArticfactURLFromARMJSON(requestObject) : GetArtifactURLFromJSON(requestObject);
                }
                catch (Exception ex)
                {
                    return ArmUtils.CreateErrorResponse(Request, HttpStatusCode.BadRequest, ex);
                }
            }
            // For zip artifacts (zipdeploy, wardeploy, onedeploy with type=zip), copy the request body in a temp zip file.
            // It will be extracted to the appropriate directory by the Fetch handler
            else if (artifactType == ArtifactType.Zip)
            {
                await _deploymentLock.LockHttpOperationAsync(async () =>
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
                }, "Preparing zip package");
            }
            // Copy the request body to a temp file.
            // It will be moved to the appropriate directory by the Fetch handler
            else if (deploymentInfo.Deployer == Constants.OneDeploy)
            {
                await _deploymentLock.LockHttpOperationAsync(async () =>
                {
                    var artifactTempPath = Path.Combine(_environment.ZipTempPath, deploymentInfo.TargetFileName);
                    using (_tracer.Step("Saving request content to {0}", artifactTempPath))
                    {
                        await content.CopyToAsync(artifactTempPath, _tracer);
                    }

                    deploymentInfo.RepositoryUrl = artifactTempPath;
                }, "Preparing zip package");
            }

            isAsync = ArmUtils.IsArmRequest(Request) ? true : isAsync;

            var result = await _deploymentManager.FetchDeploy(deploymentInfo, isAsync, Request.GetRequestUri(), "HEAD");

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
                        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(ScmHostingConfigurations.ArmRetryAfterSeconds));
                    }
                    else if (isAsync)
                    {
                        // latest deployment keyword reserved to poll till deployment done
                        response.Headers.Location = new Uri(Request.GetRequestUri(),
                            String.Format("/api/deployments/{0}?deployer={1}&time={2}", Constants.LatestDeployment, deploymentInfo.Deployer, DateTime.UtcNow.ToString("yyy-MM-dd_HH-mm-ssZ")));
                        response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(ScmHostingConfigurations.ArmRetryAfterSeconds));
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

            // Add deploymentId to header for deployment status API
            if (deploymentInfo != null
                && !string.IsNullOrEmpty(deploymentInfo.DeploymentTrackingId))
            {
                response.Headers.Add(Constants.ScmDeploymentIdHeader, deploymentInfo.DeploymentTrackingId);
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
            if (_settings.RunFromLocalZip() && deploymentInfo is ArtifactDeploymentInfo)
            {
                ArtifactDeploymentInfo zipDeploymentInfo = (ArtifactDeploymentInfo)deploymentInfo;
                // If this was a request with a Zip URL in the JSON, we first need to get the zip content and write it to the site.
                if (!string.IsNullOrEmpty(zipDeploymentInfo.RemoteURL))
                {
                    await WriteSitePackageZip(zipDeploymentInfo, tracer, await DeploymentHelper.GetArtifactContentFromURL(zipDeploymentInfo, tracer));
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

        private void ExtractTriggers(IRepository repository, ArtifactDeploymentInfo deploymentInfo)
        {
            FileSystemHelpers.EnsureDirectory(deploymentInfo.SyncFunctionsTriggersPath);
            // Loading the zip file depends on how fast the file system is.
            // Tested Azure Files share with a zip containing 120k files (160 MBs)
            // takes 20 seconds to load. On my machine it takes 900 msec.
            using (var zip = ZipFile.OpenRead(Path.Combine(_environment.SitePackagesPath, deploymentInfo.ArtifactFileName)))
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
                    var path = Path.Combine(deploymentInfo.SyncFunctionsTriggersPath, entry.FullName);
                    FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(path));
                    entry.ExtractToFile(path, overwrite: true);
                }
            }

            CommitRepo(repository, deploymentInfo);

            bool isFunctionJson(string fullName)
            {
                return fullName.EndsWith(Constants.FunctionsConfigFile) &&
                    fullName.Count(c => c == '/' || c == '\\') == 1;
            }
        }

        // OneDeploy Fetch handler for non-zip artifacts.
        // For zip files, OneDeploy uses the LocalZipHandler Fetch handler
        // NOTE: Do not access the request stream as it may have been closed during asynchronous scenarios
        private async Task OneDeployFetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            var artifactDeploymentInfo = (ArtifactDeploymentInfo) deploymentInfo;

            // This is the path where the artifact being deployed is staged, before it is copied to the final target location
            var artifactDirectoryStagingPath = repository.RepositoryPath;

            var targetInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(artifactDirectoryStagingPath);
            if (targetInfo.Exists)
            {
                // If tempDirPath already exists, rename it so we can delete it later
                var moveTarget = Path.Combine(targetInfo.Parent.FullName, Path.GetRandomFileName());
                using (tracer.Step(string.Format("Renaming ({0}) to ({1})", targetInfo.FullName, moveTarget)))
                {
                    targetInfo.MoveTo(moveTarget);
                }
            }

            //
            // We want to create a directory structure under 'artifactDirectoryStagingPath'
            // such that it exactly matches the directory structure specified
            // by deploymentInfo.TargetSubDirectoryRelativePath
            //
            string stagingSubDirPath = artifactDirectoryStagingPath;

            if (!string.IsNullOrWhiteSpace(artifactDeploymentInfo.TargetSubDirectoryRelativePath))
            {
                stagingSubDirPath = Path.Combine(artifactDirectoryStagingPath, artifactDeploymentInfo.TargetSubDirectoryRelativePath);
            }

            // Create artifact staging directory hierarchy before later use
            Directory.CreateDirectory(stagingSubDirPath);

            var artifactFileStagingPath = Path.Combine(stagingSubDirPath, deploymentInfo.TargetFileName);

            // If RemoteUrl is non-null, it means the content needs to be downloaded from the Url source to the staging location
            // Else, it had been downloaded already so we just move the downloaded file to the staging location
            if (!string.IsNullOrWhiteSpace(artifactDeploymentInfo.RemoteURL))
            {
                using (tracer.Step("Saving request content to {0}", artifactFileStagingPath))
                {
                    var content = await DeploymentHelper.GetArtifactContentFromURL(artifactDeploymentInfo, tracer);
                    var copyTask = content.CopyToAsync(artifactFileStagingPath, tracer);

                    // Deletes all files and directories except for artifactFileStagingPath and artifactDirectoryStagingPath
                    var cleanTask = Task.Run(() => DeleteFilesAndDirsExcept(artifactFileStagingPath, artifactDirectoryStagingPath, tracer));

                    // Lets the copy and cleanup tasks to run in parallel and wait for them to finish
                    await Task.WhenAll(copyTask, cleanTask);
                }
            }
            else
            {
                var srcInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(deploymentInfo.RepositoryUrl);
                using (tracer.Step(string.Format("Moving {0} to {1}", targetInfo.FullName, artifactFileStagingPath)))
                {
                    srcInfo.MoveTo(artifactFileStagingPath);
                }

                // Deletes all files and directories except for artifactFileStagingPath and artifactDirectoryStagingPath
                DeleteFilesAndDirsExcept(artifactFileStagingPath, artifactDirectoryStagingPath, tracer);
            }

            // The deployment flow expects at least 1 commit in the IRepository commit, refer to CommitRepo() for more info
            CommitRepo(repository, artifactDeploymentInfo);
        }

        private async Task<string> DeployZipLocally(ArtifactDeploymentInfo zipDeploymentInfo, ITracer tracer)
        {
            var content = await DeploymentHelper.GetArtifactContentFromURL(zipDeploymentInfo, tracer);
            var zipFileName = Path.ChangeExtension(Path.GetRandomFileName(), "zip");
            var zipFilePath = Path.Combine(_environment.ZipTempPath, zipFileName);

            using (_tracer.Step("Downloading content from {0} to {1}", zipDeploymentInfo.RemoteURL.Split('?')[0], zipFilePath))
            {
                await content.CopyToAsync(zipFilePath, _tracer);
            }

            zipDeploymentInfo.RepositoryUrl = zipFilePath;
            return zipFilePath;
        }

        private async Task LocalZipFetch(IRepository repository, DeploymentInfoBase deploymentInfo, string targetBranch, ILogger logger, ITracer tracer)
        {
            var zipDeploymentInfo = (ArtifactDeploymentInfo)deploymentInfo;

            // If this was a request with a Zip URL in the JSON, we need to deploy the zip locally and get the path
            // Otherwise, for this kind of deployment, RepositoryUrl is a local path.
            var sourceZipFile = !string.IsNullOrEmpty(zipDeploymentInfo.RemoteURL)
                ? await DeployZipLocally(zipDeploymentInfo, tracer)
                : zipDeploymentInfo.RepositoryUrl;

            var artifactFileStagingDirectory = repository.RepositoryPath;

            var info = FileSystemHelpers.FileInfoFromFileName(sourceZipFile);
            var sizeInMb = (info.Length / (1024f * 1024f)).ToString("0.00", CultureInfo.InvariantCulture);

            var message = String.Format(
                CultureInfo.InvariantCulture,
                "Cleaning up temp folders from previous zip deployments and extracting pushed zip file {0} ({1} MB) to {2}",
                info.FullName,
                sizeInMb,
                artifactFileStagingDirectory);

            logger.Log(message);

            using (tracer.Step(message))
            {
                // If extractTargetDirectory already exists, rename it so we can delete it concurrently with
                // the unzip (along with any other junk in the folder)
                var targetInfo = FileSystemHelpers.DirectoryInfoFromDirectoryName(artifactFileStagingDirectory);
                if (targetInfo.Exists)
                {
                    var moveTarget = Path.Combine(targetInfo.Parent.FullName, Path.GetRandomFileName());
                    using (tracer.Step(string.Format("Renaming extractTargetDirectory({0}) to tempDirectory({1})", targetInfo.FullName, moveTarget)))
                    {
                        targetInfo.MoveTo(moveTarget);
                    }
                }

                var cleanTask = Task.Run(() => DeleteFilesAndDirsExcept(sourceZipFile, artifactFileStagingDirectory, tracer));
                var extractTask = Task.Run(() =>
                {
                    //
                    // We want to create a directory structure under 'artifactFileStagingDirectory'
                    // such that it exactly matches the directory structure specified
                    // by deploymentInfo.TargetSubDirectoryRelativePath
                    //
                    string extractSubDirectoryPath = artifactFileStagingDirectory;

                    if (!string.IsNullOrWhiteSpace(deploymentInfo.TargetSubDirectoryRelativePath) && deploymentInfo.Deployer == Constants.OneDeploy)
                    {
                        extractSubDirectoryPath = Path.Combine(artifactFileStagingDirectory, deploymentInfo.TargetSubDirectoryRelativePath);
                    }

                    FileSystemHelpers.CreateDirectory(extractSubDirectoryPath);

                    using (var file = info.OpenRead())
                    using (var zip = new ZipArchive(file, ZipArchiveMode.Read))
                    {
                        zip.Extract(extractSubDirectoryPath, tracer, _settings.GetZipDeployDoNotPreserveFileTime());
                    }
                });

                await Task.WhenAll(cleanTask, extractTask);
            }
            CommitRepo(repository, zipDeploymentInfo);
        }

        private static void CommitRepo(IRepository repository, ArtifactDeploymentInfo deploymentInfo)
        {
            // Needed in order for repository.GetChangeSet() to work.
            // Similar to what OneDriveHelper and DropBoxHelper do.
            // We need to make to call repository.Commit() since deployment flow expects at
            // least 1 commit in the IRepository. Even though there is no repo per se in this
            // scenario, deployment pipeline still generates a NullRepository
            repository.Commit(deploymentInfo.Message, deploymentInfo.Author, deploymentInfo.AuthorEmail);
        }

        private async Task WriteSitePackageZip(ArtifactDeploymentInfo zipDeploymentInfo, ITracer tracer, HttpContent content)
        {
            var filePath = Path.Combine(_environment.SitePackagesPath, zipDeploymentInfo.ArtifactFileName);

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
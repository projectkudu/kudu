using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Deployment
{
    public class DeploymentController : ApiController
    {
        private static DeploymentsCacheItem _cachedDeployments = DeploymentsCacheItem.None;

        private readonly IEnvironment _environment;
        private readonly IAnalytics _analytics;
        private readonly IDeploymentManager _deploymentManager;
        private readonly IDeploymentStatusManager _status;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly IAutoSwapHandler _autoSwapHandler;

        public DeploymentController(ITracer tracer,
                                    IEnvironment environment,
                                    IAnalytics analytics,
                                    IDeploymentManager deploymentManager,
                                    IDeploymentStatusManager status,
                                    IOperationLock deploymentLock,
                                    IRepositoryFactory repositoryFactory,
                                    IAutoSwapHandler autoSwapHandler)
        {
            _tracer = tracer;
            _environment = environment;
            _analytics = analytics;
            _deploymentManager = deploymentManager;
            _status = status;
            _deploymentLock = deploymentLock;
            _repositoryFactory = repositoryFactory;
            _autoSwapHandler = autoSwapHandler;
        }

        /// <summary>
        /// Delete a deployment
        /// </summary>
        /// <param name="id">id of the deployment to delete</param>
        [HttpDelete]
        public void Delete(string id)
        {
            using (_tracer.Step("DeploymentService.Delete"))
            {
                _deploymentLock.LockHttpOperation(() =>
                {
                    try
                    {
                        _deploymentManager.Delete(id);
                    }
                    catch (DirectoryNotFoundException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
                    }
                    catch (InvalidOperationException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, ex));
                    }
                });
            }
        }

        /// <summary>
        /// Deploy a previous deployment
        /// </summary>
        /// <param name="id">id of the deployment to redeploy</param>
        [HttpPut]
        public async Task<HttpResponseMessage> Deploy(string id = null)
        {
            JObject result = GetJsonContent();

            // Just block here to read the json payload from the body
            using (_tracer.Step("DeploymentService.Deploy(id)"))
            {
                HttpResponseMessage response = Request.CreateResponse(HttpStatusCode.OK);
                await _deploymentLock.LockHttpOperationAsync(async () =>
                {
                    try
                    {
                        if (_autoSwapHandler.IsAutoSwapOngoing())
                        {
                            throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, Resources.Error_AutoSwapDeploymentOngoing));
                        }

                        DeployResult deployResult;
                        if (TryParseDeployResult(id, result, out deployResult))
                        {
                            using (_tracer.Step("DeploymentService.Create(id)"))
                            {
                                CreateDeployment(deployResult, result.Value<string>("details"));

                                deployResult.Url = Request.RequestUri;
                                deployResult.LogUrl = UriHelper.MakeRelative(Request.RequestUri, "log");

                                response = Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(deployResult, Request));
                                return;
                            }
                        }

                        bool clean = false;
                        bool needFileUpdate = true;

                        if (result != null)
                        {
                            clean = result.Value<bool>("clean");
                            JToken needFileUpdateToken;
                            if (result.TryGetValue("needFileUpdate", out needFileUpdateToken))
                                needFileUpdate = needFileUpdateToken.Value<bool>();
                        }

                        string username = null;
                        AuthUtility.TryExtractBasicAuthUser(Request, out username);

                        IRepository repository = _repositoryFactory.GetRepository();
                        if (repository == null)
                        {
                            throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, Resources.Error_RepositoryNotFound));
                        }
                        ChangeSet changeSet = null;
                        if (!String.IsNullOrEmpty(id))
                        {
                            changeSet = repository.GetChangeSet(id);
                            if (changeSet == null)
                            {
                                string message = String.Format(CultureInfo.CurrentCulture, Resources.Error_DeploymentNotFound, id);
                                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, message));
                            }
                        }

                        await _deploymentManager.DeployAsync(repository, changeSet, username, clean, needFileUpdate);
                    }
                    catch (FileNotFoundException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
                    }
                });

                return response;
            }
        }

        public void CreateDeployment(DeployResult deployResult, string details)
        {
            var id = deployResult.Id;
            string path = Path.Combine(_environment.DeploymentsPath, id);
            IDeploymentStatusFile statusFile = _status.Open(id);
            if (statusFile != null)
            {
                throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.Conflict, String.Format("Deployment with id '{0}' exists", id)));
            }

            FileSystemHelpers.EnsureDirectory(path);
            statusFile = _status.Create(id);
            statusFile.Status = deployResult.Status;
            statusFile.Message = deployResult.Message;
            statusFile.Deployer = deployResult.Deployer;
            statusFile.Author = deployResult.Author;
            statusFile.AuthorEmail = deployResult.AuthorEmail;
            statusFile.StartTime = deployResult.StartTime;
            statusFile.EndTime = deployResult.EndTime;

            // miscellaneous
            statusFile.Complete = true;
            statusFile.IsReadOnly = true;
            statusFile.IsTemporary = false;
            statusFile.ReceivedTime = deployResult.StartTime;
            // keep it simple regardless of success or failure
            statusFile.LastSuccessEndTime = deployResult.EndTime;
            statusFile.Save();

            if (deployResult.Current)
            {
                _status.ActiveDeploymentId = id;
            }

            var logger = new StructuredTextLogger(Path.Combine(path, DeploymentManager.TextLogFile), _analytics);
            ILogger innerLogger;
            if (deployResult.Status == DeployStatus.Success)
            {
                innerLogger = logger.Log("Deployment successful.");
            }
            else
            {
                innerLogger = logger.Log("Deployment failed.", LogEntryType.Error);
            }

            if (!String.IsNullOrEmpty(details))
            {
                innerLogger.Log(details);
            }
        }

        public bool TryParseDeployResult(string id, JObject payload, out DeployResult deployResult)
        {
            deployResult = null;
            if (String.IsNullOrEmpty(id) || payload == null)
            {
                return false;
            }

            var status = payload.Value<int?>("status");
            if (status == null || (status.Value != 3 && status.Value != 4))
            {
                return false;
            }

            var message = payload.Value<string>("message");
            if (String.IsNullOrEmpty(message))
            {
                return false;
            }

            var deployer = payload.Value<string>("deployer");
            if (String.IsNullOrEmpty(deployer))
            {
                return false;
            }

            var author = payload.Value<string>("author");
            if (String.IsNullOrEmpty(author))
            {
                return false;
            }

            deployResult = new DeployResult
            {
                Id = id,
                Status = (DeployStatus)status.Value,
                Message = message,
                Deployer = deployer,
                Author = author
            };

            // optionals
            var now = DateTime.UtcNow;
            deployResult.AuthorEmail = payload.Value<string>("author_email");
            deployResult.StartTime = payload.Value<DateTime?>("start_time") ?? now;
            deployResult.EndTime = payload.Value<DateTime?>("end_time") ?? now;

            // only success status can be active
            var active = payload.Value<bool?>("active");
            if (active == null)
            {
                deployResult.Current = deployResult.Status == DeployStatus.Success;
            }
            else
            {
                if (active.Value && deployResult.Status != DeployStatus.Success)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.BadRequest, "Only successful status can be active!"));
                }

                deployResult.Current = active.Value;
            }

            return true;
        }

        /// <summary>
        /// Get the list of all deployments
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetDeployResults()
        {
            HttpResponseMessage response;
            EntityTagHeaderValue currentEtag = null;
            DeploymentsCacheItem cachedDeployments = _cachedDeployments;

            using (_tracer.Step("DeploymentService.GetCurrentEtag"))
            {
                currentEtag = GetCurrentEtag(Request);
                _tracer.Trace("Current Etag: {0}, Cached Etag: {1}", currentEtag, cachedDeployments.Etag);
            }

            if (EtagEquals(Request, currentEtag))
            {
                response = Request.CreateResponse(HttpStatusCode.NotModified);
            }
            else
            {
                using (_tracer.Step("DeploymentService.GetDeployResults"))
                {
                    if (!currentEtag.Equals(cachedDeployments.Etag))
                    {
                        cachedDeployments = new DeploymentsCacheItem
                        {
                            Results = GetResults(Request).ToList(),
                            Etag = currentEtag
                        };

                        _cachedDeployments = cachedDeployments;
                    }

                    response = Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(cachedDeployments.Results, Request));
                }
            }

            // return etag
            response.Headers.ETag = currentEtag;

            return response;
        }

        /// <summary>
        /// Get the list of log entries for a deployment
        /// </summary>
        /// <param name="id">id of the deployment</param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetLogEntry(string id)
        {
            using (_tracer.Step("DeploymentService.GetLogEntry"))
            {
                try
                {
                    var deployments = _deploymentManager.GetLogEntries(id).ToList();
                    foreach (var entry in deployments)
                    {
                        if (entry.HasDetails)
                        {
                            entry.DetailsUrl = UriHelper.MakeRelative(Request.RequestUri, entry.Id);
                        }
                    }

                    return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(deployments, Request));
                }
                catch (FileNotFoundException ex)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
                }
            }
        }

        /// <summary>
        /// Get the list of log entry details for a log entry
        /// </summary>
        /// <param name="id">id of the deployment</param>
        /// <param name="logId">id of the log entry</param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetLogEntryDetails(string id, string logId)
        {
            using (_tracer.Step("DeploymentService.GetLogEntryDetails"))
            {
                try
                {
                    var details = _deploymentManager.GetLogEntryDetails(id, logId).ToList();
                    return details.Any()
                        ? Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(details, Request))
                        : Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture,
                        Resources.Error_LogDetailsNotFound,
                        logId,
                        id));
                }
                catch (FileNotFoundException ex)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
                }
            }
        }

        /// <summary>
        /// Get a deployment
        /// </summary>
        /// <param name="id">id of the deployment</param>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetResult(string id)
        {
            using (_tracer.Step("DeploymentService.GetResult"))
            {
                DeployResult pending;
                if (IsLatestPendingDeployment(ref id, out pending))
                {
                    var response = Request.CreateResponse(HttpStatusCode.Accepted, ArmUtils.AddEnvelopeOnArmRequest(pending, Request));
                    response.Headers.Location = Request.RequestUri;
                    return response;
                }

                DeployResult result = _deploymentManager.GetResult(id);

                if (result == null)
                {
                    var response = Request.CreateErrorResponse(HttpStatusCode.NotFound, String.Format(CultureInfo.CurrentCulture,
                                                                       Resources.Error_DeploymentNotFound,
                                                                       id));
                    throw new HttpResponseException(response);
                }

                result.Url = Request.RequestUri;
                result.LogUrl = UriHelper.MakeRelative(Request.RequestUri, "log");

                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(result, Request));
            }
        }

        private bool IsLatestPendingDeployment(ref string id, out DeployResult pending)
        {
            if (String.Equals(Constants.LatestDeployment, id))
            {
                using (_tracer.Step("DeploymentService.GetLatestDeployment"))
                {
                    var results = _deploymentManager.GetResults();
                    pending = results.Where(r => r.Status != DeployStatus.Success && r.Status != DeployStatus.Failed).FirstOrDefault();
                    if (pending != null)
                    {
                        _tracer.Trace("Deployment {0} is {1}", pending.Id, pending.Status);
                        return true;
                    }

                    var latest = results.Where(r => r.EndTime != null).OrderBy(r => r.EndTime.Value).LastOrDefault();
                    if (latest != null)
                    {
                        _tracer.Trace("Deployment {0} is {1} at {2}", latest.Id, latest.Status, latest.EndTime.Value.ToString("o"));

                        id = latest.Id;
                    }
                    else
                    {
                        _tracer.Trace("Could not find latest deployment!");
                    }
                }
            }

            pending = null;
            return false;
        }

        /// <summary>
        /// Get the list of all deployments
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetDeploymentScript()
        {
            using (_tracer.Step("DeploymentService.GetDeploymentScript"))
            {
                if (!_deploymentManager.GetResults().Any())
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, "Need to deploy website to get deployment script."));
                }

                string deploymentScriptContent = _deploymentManager.GetDeploymentScriptContent();

                if (deploymentScriptContent == null)
                {
                    throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, "Operation only supported if not using a custom deployment script"));
                }

                HttpResponseMessage response = Request.CreateResponse();
                response.Content = ZipStreamContent.Create("deploymentscript.zip", _tracer, zip =>
                {
                    // Add deploy.cmd to zip file
                    zip.AddFile(DeploymentManager.DeploymentScriptFileName, deploymentScriptContent);

                    // Add .deployment to cmd file
                    zip.AddFile(DeploymentSettingsProvider.DeployConfigFile, "[config]\ncommand = {0}\n".FormatInvariant(DeploymentManager.DeploymentScriptFileName));
                });

                return response;
            }
        }

        private EntityTagHeaderValue GetCurrentEtag(HttpRequestMessage request)
        {
            return new EntityTagHeaderValue(String.Format("\"{0:x}\"", request.RequestUri.PathAndQuery.GetHashCode() ^ _status.LastModifiedTime.Ticks));
        }

        private static bool EtagEquals(HttpRequestMessage request, EntityTagHeaderValue currentEtag)
        {
            return request.Headers.IfNoneMatch != null &&
                request.Headers.IfNoneMatch.Any(etag => currentEtag.Equals(etag));
        }

        private IEnumerable<DeployResult> GetResults(HttpRequestMessage request)
        {
            foreach (var result in _deploymentManager.GetResults())
            {
                result.Url = UriHelper.MakeRelative(request.RequestUri, result.Id);
                result.LogUrl = UriHelper.MakeRelative(request.RequestUri, result.Id + "/log");
                yield return result;
            }
        }

        private JObject GetJsonContent()
        {
            try
            {
                var payload = Request.Content.ReadAsAsync<JObject>().Result;
                if (ArmUtils.IsArmRequest(Request))
                {
                    payload = payload.Value<JObject>("properties");
                }

                return payload;
            }
            catch
            {
                // We're going to return null here since we don't want to force a breaking change
                // on the client side. If the incoming request isn't application/json, we want this
                // to return null.
                return null;
            }
        }

        class DeploymentsCacheItem
        {
            public readonly static DeploymentsCacheItem None = new DeploymentsCacheItem();

            public List<DeployResult> Results { get; set; }

            public EntityTagHeaderValue Etag { get; set; }
        }
    }
}

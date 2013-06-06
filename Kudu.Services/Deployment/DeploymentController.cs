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
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Services.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Deployment
{
    public class DeploymentController : ApiController
    {
        private readonly IDeploymentManager _deploymentManager;
        private readonly IDeploymentStatusManager _status;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IRepositoryFactory _repositoryFactory;

        public DeploymentController(ITracer tracer,
                                    IDeploymentManager deploymentManager,
                                    IDeploymentStatusManager status,
                                    IOperationLock deploymentLock,
                                    IRepositoryFactory repositoryFactory)
        {
            _tracer = tracer;
            _deploymentManager = deploymentManager;
            _status = status;
            _deploymentLock = deploymentLock;
            _repositoryFactory = repositoryFactory;
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
        public async Task Deploy(string id = null)
        {
            JObject result = GetJsonContent();

            // Just block here to read the json payload from the body
            using (_tracer.Step("DeploymentService.Deploy(id)"))
            {
                await _deploymentLock.LockHttpOperationAsync(async () =>
                {
                    try
                    {
                        bool clean = false;

                        if (result != null)
                        {
                            clean = result.Value<bool>("clean");
                        }

                        string username = null;
                        AuthUtility.TryExtractBasicAuthUser(Request, out username);

                        IRepository repository = _repositoryFactory.GetRepository();
                        if (repository == null)
                        {
                            throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, Resources.Error_RepositoryNotFound));
                        }

                        await _deploymentManager.DeployAsync(repository, id, username, clean);
                    }
                    catch (FileNotFoundException ex)
                    {
                        throw new HttpResponseException(Request.CreateErrorResponse(HttpStatusCode.NotFound, ex));
                    }
                });
            }
        }

        /// <summary>
        /// Get the list of all deployments
        /// </summary>
        /// <returns></returns>
        [HttpGet]
        public HttpResponseMessage GetDeployResults()
        {
            HttpResponseMessage response;
            EntityTagHeaderValue currentEtag = GetCurrentEtag(Request);

            if (EtagEquals(Request, currentEtag))
            {
                response = Request.CreateResponse(HttpStatusCode.NotModified);
            }
            else
            {
                using (_tracer.Step("DeploymentService.GetDeployResults"))
                {
                    IEnumerable<DeployResult> results = GetResults(Request).ToList();
                    response = Request.CreateResponse(HttpStatusCode.OK, results);
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
        public IEnumerable<LogEntry> GetLogEntry(string id)
        {
            using (_tracer.Step("DeploymentService.GetLogEntry"))
            {
                try
                {
                    IEnumerable<LogEntry> deployments = _deploymentManager.GetLogEntries(id).ToList();
                    foreach (var entry in deployments)
                    {
                        if (entry.HasDetails)
                        {
                            entry.DetailsUrl = UriHelper.MakeRelative(Request.RequestUri, entry.Id);
                        }
                    }

                    return deployments;
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
        public IEnumerable<LogEntry> GetLogEntryDetails(string id, string logId)
        {
            using (_tracer.Step("DeploymentService.GetLogEntryDetails"))
            {
                try
                {
                    return _deploymentManager.GetLogEntryDetails(id, logId).ToList();
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
        public DeployResult GetResult(string id)
        {
            using (_tracer.Step("DeploymentService.GetResult"))
            {
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

                return result;
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
                return Request.Content.ReadAsAsync<JObject>().Result;
            }
            catch
            {
                // We're going to return null here since we don't want to force a breaking change
                // on the client side. If the incoming request isn't application/json, we want this 
                // to return null.
                return null;
            }
        }
    }
}

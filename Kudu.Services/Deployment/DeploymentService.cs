using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Json;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Services.Infrastructure;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.Deployment
{
    [ServiceContract]
    public class DeploymentService
    {
        private readonly IDeploymentManager _deploymentManager;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;

        public DeploymentService(ITracer tracer, 
                                 IDeploymentManager deploymentManager, 
                                 IOperationLock deploymentLock)
        {
            _tracer = tracer;
            _deploymentManager = deploymentManager;
            _deploymentLock = deploymentLock;
        }

        [Description("Deletes a deployment.")]
        [WebInvoke(UriTemplate = "{id}", Method = "DELETE")]
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
                        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                        response.Content = new StringContent(ex.Message);
                        throw new HttpResponseException(response);
                    }
                    catch (InvalidOperationException ex)
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.Conflict);
                        response.Content = new StringContent(ex.Message);
                        throw new HttpResponseException(response);
                    }
                });
            }
        }

        [Description("Deploys a specific deployment based on its id.")]
        [WebInvoke(Method = "PUT", UriTemplate = "{id}")]
        public void Deploy(HttpRequestMessage request, string id)
        {
            // Just block here to read the json payload from the body
            var result = request.Content.ReadAsAsync<JsonValue>().Result;
            using (_tracer.Step("DeploymentService.Deploy(id)"))
            {
                _deploymentLock.LockHttpOperation(() =>
                {
                    try
                    {
                        bool clean = false;

                        if (result != null)
                        {
                            JsonValue cleanValue = result["clean"];
                            clean = cleanValue != null && cleanValue.ReadAs<bool>();
                        }

                        _deploymentManager.Deploy(id, clean);
                    }
                    catch (FileNotFoundException ex)
                    {
                        var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                        response.Content = new StringContent(ex.Message);
                        throw new HttpResponseException(response);
                    }
                });
            }
        }

        [Description("Gets the deployment results of all deployments.")]
        [WebGet(UriTemplate = "")]
        public IQueryable<DeployResult> GetDeployResults(HttpRequestMessage request)
        {
            using (_tracer.Step("DeploymentService.GetDeployResults"))
            {
                return GetResults(request).AsQueryable();
            }
        }

        [Description("Gets the log of a specific deployment based on its id.")]
        [WebGet(UriTemplate = "{id}/log")]
        public IEnumerable<LogEntry> GetLogEntry(HttpRequestMessage request, string id)
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
                            entry.DetailsUrl = UriHelper.MakeRelative(request.RequestUri, entry.Id);
                        }
                    }

                    return deployments;
                }
                catch (FileNotFoundException ex)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                    response.Content = new StringContent(ex.Message);
                    throw new HttpResponseException(response);
                }
            }
        }

        [Description("Gets the specified log entry details.")]
        [WebGet(UriTemplate = "{id}/log/{logId}")]
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
                    var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                    response.Content = new StringContent(ex.Message);
                    throw new HttpResponseException(response);
                }
            }
        }

        [Description("Gets the deployment result of a specific deployment based on its id.")]
        [WebGet(UriTemplate = "{id}")]
        public DeployResult GetResult(HttpRequestMessage request, string id)
        {
            using (_tracer.Step("DeploymentService.GetResult"))
            {
                DeployResult result = _deploymentManager.GetResult(id);

                if (result == null)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                    response.Content = new StringContent(String.Format(CultureInfo.CurrentCulture, 
                                                                       Resources.Error_DeploymentNotFound, 
                                                                       id));
                    throw new HttpResponseException(response);
                }

                result.Url = request.RequestUri;
                result.LogUrl = UriHelper.MakeRelative(request.RequestUri, "log");

                return result;
            }
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
    }
}

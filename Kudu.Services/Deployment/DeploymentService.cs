using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;
using Kudu.Contracts;
using Kudu.Core.Deployment;
using Kudu.Services.Infrastructure;
using Microsoft.ApplicationServer.Http.Dispatcher;

namespace Kudu.Services.Deployment
{
    [ServiceContract]
    public class DeploymentService
    {
        private readonly IDeploymentManager _deploymentManager;
        private readonly IProfiler _profiler;

        public DeploymentService(IProfiler profiler, IDeploymentManager deploymentManager)
        {
            _profiler = profiler;
            _deploymentManager = deploymentManager;
        }

        [Description("Deletes a deployment.")]
        [WebInvoke(UriTemplate = "{id}", Method = "DELETE")]
        public void Delete(string id)
        {
            using (_profiler.Step("DeploymentService.Delete"))
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
            }
        }

        [Description("Deploys a specific deployment based on its id.")]
        [WebInvoke(Method = "PUT", UriTemplate = "{id}")]
        public void Deploy(string id)
        {
            using (_profiler.Step("DeploymentService.Deploy(id)"))
            {
                try
                {
                    _deploymentManager.Deploy(id);
                }
                catch (FileNotFoundException ex)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                    response.Content = new StringContent(ex.Message);
                    throw new HttpResponseException(response);
                }
            }
        }

        [Description("Gets the deployment results of all deployments.")]
        [WebGet(UriTemplate = "")]
        public IEnumerable<DeployResult> GetDeployResults(HttpRequestMessage request)
        {
            using (_profiler.Step("DeploymentService.GetDeployResults"))
            {
                foreach (var result in _deploymentManager.GetResults())
                {
                    SetUrls(request, result);
                    yield return result;
                }
            }
        }

        [Description("Gets the log of a specific deployment based on its id.")]
        [WebGet(UriTemplate = "{id}/log")]
        public IEnumerable<LogEntry> GetLogEntry(HttpRequestMessage request, string id)
        {
            using (_profiler.Step("DeploymentService.GetLogEntry"))
            {
                try
                {
                    var deployments = _deploymentManager.GetLogEntries(id).ToList();
                    foreach (var entry in deployments)
                    {
                        entry.DetailsUrl = UriHelper.MakeRelative(request.RequestUri, entry.Id);
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
            using (_profiler.Step("DeploymentService.GetLogEntryDetails"))
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
            using (_profiler.Step("DeploymentService.GetResult"))
            {
                DeployResult result = _deploymentManager.GetResult(id);

                if (result == null)
                {
                    var response = new HttpResponseMessage(HttpStatusCode.NotFound);
                    response.Content = new StringContent(String.Format("Deployment '{0}' not found.", id));
                    throw new HttpResponseException(response);
                }

                SetUrls(request, result);

                return result;
            }
        }

        private static void SetUrls(HttpRequestMessage request, DeployResult result)
        {
            result.Url = UriHelper.MakeRelative(request.RequestUri, result.Id);
            result.LogUrl = UriHelper.MakeRelative(request.RequestUri, result.Id + "/log");
        }
    }
}

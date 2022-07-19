using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Diagnostics;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Infrastructure;
using System.Threading.Tasks;
using System.Collections;
using System.Text;

namespace Kudu.Services.Diagnostics
{
    public class XenonProcessController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;

        public XenonProcessController(ITracer tracer,
                                 IEnvironment environment,
                                 IDeploymentSettingsManager settings)
        {
            _tracer = tracer;
            _environment = environment;
            _settings = settings;
        }

        [HttpGet]
        public HttpResponseMessage GetThread(int processId, int threadId)
        {
            using (_tracer.Step("XenonProcessController.GetThread"))
            {
                return ForwardToContainer($"{processId}/threads/{threadId}");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllThreads(int id)
        {
            using (_tracer.Step("XenonProcessController.GetAllThreads"))
            {
                return ForwardToContainer($"{id}/threads");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetModule(int id, string baseAddress)
        {
            using (_tracer.Step("XenonProcessController.GetModule"))
            {
                return ForwardToContainer($"{id}/modules/{baseAddress}");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllModules(int id)
        {
            using (_tracer.Step("XenonProcessController.GetAllModules"))
            {
                return ForwardToContainer($"{id}/modules");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetEnvironments(int id, string filter)
        {
            using (_tracer.Step("XenonProcessController.GetEnvironments"))
            {
                return ForwardToContainer($"{id}/environments/{filter}");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllProcesses(bool allUsers = false)
        {
            var requestUri = Request.GetRequestUri(_settings.GetUseOriginalHostForReference()).GetLeftPart(UriPartial.Path).TrimEnd('/');
            using (_tracer.Step("XenonProcessController.GetAllProcesses"))
            {
                // TODO: WHAT TO DO ABOUT THE ALLUSERS PART
                return ForwardToContainer($"");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetProcess(int id)
        {
            using (_tracer.Step("XenonProcessController.GetProcess"))
            {
                return ForwardToContainer($"{id}");
            }
        }

        [HttpDelete, HttpPost]
        public HttpResponseMessage KillProcess(int id)
        {
            using (_tracer.Step("XenonProcessController.KillProcess"))
            {
                return ForwardToContainer($"{id}");
            }
        }

        [HttpGet]
        public HttpResponseMessage MiniDump(int id, int dumpType = 0, string format = null)
        {
            using (_tracer.Step("XenonProcessController.MiniDump"))
            {
                return ForwardToContainer($"{id}/dump");
            }
        }

        [HttpPost]
        public HttpResponseMessage StartProfileAsync(int id, bool iisProfiling = false)
        {
            using (_tracer.Step("XenonProcessController.StartProfileAsync"))
            {
                return ForwardToContainer($"{id}/profile/start");
            }
        }

        [HttpGet]
        public HttpResponseMessage StopProfileAsync(int id)
        {
            using (_tracer.Step("XenonProcessController.StopProfileAsync"))
            {
                return ForwardToContainer($"{id}/profile/stop");
            }
        }

        public HttpResponseMessage ForwardToContainer(string route)
        {
            using (_tracer.Step("XenonProcessController.ForwardToContainer"))
            {
                // Forward request to windows container

                // Get the container address and the port kudu agent port
                IDictionary environmentVariables = System.Environment.GetEnvironmentVariables();
                if (environmentVariables.Contains("KUDU_AGENT_HOST") && environmentVariables.Contains("KUDU_AGENT_PORT"))
                {
                    var containerAddress = environmentVariables["KUDU_AGENT_HOST"];     // TODO: This should be a try get correct?
                    var kuduContainerAgentPort = environmentVariables["KUDU_AGENT_PORT"];
                    var kudu_agent_un = environmentVariables["KUDU_AGENT_USR"];
                    var kudu_agent_pwd = environmentVariables["KUDU_AGENT_PWD"];

                    string containerUrl = $"http://{containerAddress}:{kuduContainerAgentPort}/processes/{route}";

                    // Request the kudu agent in the container to return list of processes
                    HttpClient client = new HttpClient();
                    string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{kudu_agent_un}:{kudu_agent_pwd}"));
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("BASIC", authHeader);
                    HttpResponseMessage response = client.GetAsync(containerUrl).Result;
                    if (response.IsSuccessStatusCode)
                    {
                        String urlContents = response.Content.ReadAsStringAsync().Result;
                        return Request.CreateResponse(HttpStatusCode.OK, urlContents);

                    }
                    else
                    {
                        return Request.CreateResponse(response.StatusCode, response.ReasonPhrase);
                    }
                }
                else
                {
                    return Request.CreateResponse(HttpStatusCode.NotFound); // TODO: Should there be a message saying that the container did not load properly
                }

            }
        }
    }
}

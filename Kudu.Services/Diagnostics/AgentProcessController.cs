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
    public class AgentProcessController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;

        public AgentProcessController(ITracer tracer,
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
            using (_tracer.Step("AgentProcessController.GetThread"))
            {
                return ForwardProcessRequestToContainer($"{processId}/threads/{threadId}");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllThreads(int id)
        {
            using (_tracer.Step("AgentProcessController.GetAllThreads"))
            {
                return ForwardProcessRequestToContainer($"{id}/threads");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetModule(int id, string baseAddress)
        {
            using (_tracer.Step("AgentProcessController.GetModule"))
            {
                return ForwardProcessRequestToContainer($"{id}/modules/{baseAddress}");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllModules(int id)
        {
            using (_tracer.Step("AgentProcessController.GetAllModules"))
            {
                return ForwardProcessRequestToContainer($"{id}/modules");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetEnvironments(int id, string filter)
        {
            using (_tracer.Step("AgentProcessController.GetEnvironments"))
            {
                return ForwardProcessRequestToContainer($"{id}/environments/{filter}");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetAllProcesses(bool allUsers = false)
        {
            var requestUri = Request.GetRequestUri(_settings.GetUseOriginalHostForReference()).GetLeftPart(UriPartial.Path).TrimEnd('/');
            using (_tracer.Step("AgentProcessController.GetAllProcesses"))
            {
                return ForwardProcessRequestToContainer($"");
            }
        }

        [HttpGet]
        public HttpResponseMessage GetProcess(int id)
        {
            using (_tracer.Step("AgentProcessController.GetProcess"))
            {
                return ForwardProcessRequestToContainer($"{id}");
            }
        }

        [HttpDelete, HttpPost]
        public HttpResponseMessage KillProcess(int id)
        {
            using (_tracer.Step("AgentProcessController.KillProcess"))
            {
                return ForwardProcessRequestToContainer($"{id}");
            }
        }

        [HttpGet]
        public HttpResponseMessage MiniDump(int id, int dumpType = 0, string format = null)
        {
            using (_tracer.Step("AgentProcessController.MiniDump"))
            {
                return ForwardProcessRequestToContainer($"{id}/dump");
            }
        }

        [HttpPost]
        public HttpResponseMessage StartProfileAsync(int id, bool iisProfiling = false)
        {
            using (_tracer.Step("AgentProcessController.StartProfileAsync"))
            {
                return ForwardProcessRequestToContainer($"{id}/profile/start");
            }
        }

        [HttpGet]
        public HttpResponseMessage StopProfileAsync(int id)
        {
            using (_tracer.Step("AgentProcessController.StopProfileAsync"))
            {
                return ForwardProcessRequestToContainer($"{id}/profile/stop");
            }
        }

        public HttpResponseMessage ForwardProcessRequestToContainer(string route)
        {
            using (_tracer.Step("AgentProcessController.ForwardToContainer"))
            {
                return HttpRequestExtensions.ForwardToContainer($"/processes/{route}", Request);
            }
        }
    }
}

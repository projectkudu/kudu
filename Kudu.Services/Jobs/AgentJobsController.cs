using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using System.Web;
using System.Web.Http;
using System.Web.Http.Controllers;
using Kudu.Contracts;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Tracing;
using Kudu.Core.Hooks;
using Kudu.Core.Jobs;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;

namespace Kudu.Services.Jobs
{
    [ArmControllerConfiguration]
    public class AgentJobsController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly ITriggeredJobsManager _triggeredJobsManager;
        private readonly IContinuousJobsManager _continuousJobsManager;

        public AgentJobsController(ITriggeredJobsManager triggeredJobsManager, IContinuousJobsManager continuousJobsManager, ITracer tracer)
        {
            _triggeredJobsManager = triggeredJobsManager;
            _continuousJobsManager = continuousJobsManager;
            _tracer = tracer;
        }


        [HttpGet]
        public HttpResponseMessage ListAllJobs()
        {
            return ForwardJobRequestToContainer("");
        }

        [HttpGet]
        public HttpResponseMessage ListContinuousJobs()
        {
            return ForwardJobRequestToContainer("continuouswebjobs/");
        }

        [HttpGet]
        public HttpResponseMessage GetContinuousJob(string jobName)
        {
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}");
        }

        [HttpPost]
        public HttpResponseMessage EnableContinuousJob(string jobName)
        {
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}/start");
        }

        [HttpPost]
        public HttpResponseMessage DisableContinuousJob(string jobName)
        {
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}/stop");
        }

        [HttpGet]
        public HttpResponseMessage GetContinuousJobSettings(string jobName)
        {
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}/settings");
        }

        [HttpPut]
        public HttpResponseMessage SetContinuousJobSettings(string jobName, JobSettings jobSettings)
        {
            // Re-add the settings to the request
            Request.Content = new StringContent(JsonSerializer.Serialize(jobSettings), Encoding.UTF8, "application/json");
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}/settings");
        }


        [HttpGet]
        public HttpResponseMessage ListTriggeredJobs()
        {
            return ForwardJobRequestToContainer("triggeredwebjobs/");
        }

        [HttpGet]
        public HttpResponseMessage ListTriggeredJobsInSwaggerFormat()
        {
            return ForwardJobRequestToContainer("triggeredwebjobsswagger/");
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJob(string jobName)
        {
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}");
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobHistory(string jobName)
        {
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}/history");
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobRun(string jobName, string runId)
        {
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}/history/{runId}");
        }

        [HttpPost]
        public HttpResponseMessage InvokeTriggeredJob(string jobName, string arguments = null)
        {
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}/run");
        }

        [HttpPut]
        public HttpResponseMessage CreateContinuousJob(string jobName)
        {
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}");
        }

        [HttpPut]
        public HttpResponseMessage CreateContinuousJobArm(string jobName, ArmEntry<ContinuousJob> armContinuousJob)
        {
            // OLD: return SetJobSettings(jobName, armContinuousJob.Properties.Settings, _continuousJobsManager);
            // convert arm entry continuous job to settings for a job like the function normally would?
            // What does this to string actually do? Does it create a json string like I want, or do it just print the object type
            //Request.Headers.Add("settings", armContinuousJob.Properties.Settings.ToString());
            HttpResponseMessage msg = new HttpResponseMessage(HttpStatusCode.OK);
            msg.Content = new StringContent(armContinuousJob.Properties.ToString());
            return msg;
            //return ForwardJobRequestToContainer("continuouswebjobs/{jobName}/settings");
        }

        [HttpDelete]
        public HttpResponseMessage RemoveContinuousJob(string jobName)
        {
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}");
        }

        [HttpPut]
        public HttpResponseMessage CreateTriggeredJob(string jobName)
        {
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}");
        }

        [HttpPut]
        public HttpResponseMessage CreateTriggeredJobArm(string jobName, ArmEntry<TriggeredJob> armTriggeredJob)
        {
            throw new NotImplementedException();
        }

        [HttpDelete]
        public HttpResponseMessage RemoveTriggeredJob(string jobName)
        {
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}");
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobSettings(string jobName)
        {
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}/settings");
        }

        [HttpPut]
        public HttpResponseMessage SetTriggeredJobSettings(string jobName, JobSettings jobSettings)
        {
            // Re-add the settings to the request
            Request.Content = new StringContent(JsonSerializer.Serialize(jobSettings), Encoding.UTF8, "application/json");
            return ForwardJobRequestToContainer($"triggeredwebjobs/{jobName}/settings");
        }

        [AcceptVerbs("GET", "HEAD", "PUT", "POST", "DELETE", "PATCH")]
        public HttpResponseMessage RequestPassthrough(string jobName, string path)
        {
            return ForwardJobRequestToContainer($"continuouswebjobs/{jobName}/passthrough/{path}");
        }

        private HttpResponseMessage ForwardJobRequestToContainer(string route)
        {
            using (_tracer.Step("AgentJobsController.ForwardToContainer"))
            {
                return Diagnostics.HttpRequestExtensions.ForwardToContainer($"/webjobs/{route}", Request);
            }
        }
    }
}
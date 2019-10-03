using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Core.Helpers;
using Kudu.Core.Tracing;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core;

namespace Kudu.Services.Docker
{
    public class DockerController : ApiController
    {
        private const string RESTART_REASON = "Docker CI webhook";
        private readonly ITraceFactory _traceFactory;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;

        public DockerController(ITraceFactory traceFactory, IDeploymentSettingsManager settings, IEnvironment environment)
        {
            _traceFactory = traceFactory;
            _settings = settings;
            _environment = environment;
        }

        [HttpPost]
        public HttpResponseMessage ReceiveHook()
        {
            if (OSDetector.IsOnWindows() && !EnvironmentHelper.IsWindowsContainers())
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("Docker.ReceiveWebhook"))
            {
                try
                {
                    if (_settings.IsDockerCiEnabled())
                    {
                        DockerContainerRestartTrigger.RequestContainerRestart(_environment, RESTART_REASON, tracer);
                    }
                }
                catch (Exception e)
                {
                    return Request.CreateResponse(HttpStatusCode.InternalServerError, e.Message); 
                }
            }

            return Request.CreateResponse(HttpStatusCode.OK);
        }
    }
}

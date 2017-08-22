using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Core.Helpers;
using Kudu.Core.Tracing;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;

namespace Kudu.Services.Docker
{
    public class DockerController : ApiController
    {
        private const string RESTART_REASON = "Docker CI webhook";
        private readonly ITraceFactory _traceFactory;
        private readonly IDeploymentSettingsManager _settings;

        public DockerController(ITraceFactory traceFactory, IDeploymentSettingsManager settings)
        {
            _traceFactory = traceFactory;
            _settings = settings;
        }

        [HttpPost]
        public HttpResponseMessage ReceiveHook()
        {
            if (OSDetector.IsOnWindows())
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("Docker.SetDockerTimestamp"))
            {
                try
                {
                    if (_settings.IsDockerCiEnabled())
                    {
                        LinuxContainerRestartTrigger.RequestContainerRestart(RESTART_REASON);
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

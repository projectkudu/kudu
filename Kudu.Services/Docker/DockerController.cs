using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Kudu.Core;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

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
        public async Task<HttpResponseMessage> ReceiveHook()
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
                        DockerContainerRestartTrigger.RequestContainerRestart(_environment, RESTART_REASON);


                        // When a docker image is updated, we need to ensure that a SyncTriggers operation is performed.
                        // There are 2 cases:
                        // 1) No containers are currently running - in this case the sync will start a container with the new image,
                        //    and the sync will be up to date.
                        // 2) One or more containers are currently running - in this case the container restart will cause them to
                        //    restart, and the background sync triggers performed by the runtime will do the sync.
                        await FunctionsHelper.SyncTriggersAsync(tracer);
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

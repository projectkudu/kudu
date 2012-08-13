using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Newtonsoft.Json.Linq;

namespace Kudu.Services
{
    public class EnvironmentController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IEnvironment _environment;
        private static readonly string _version = typeof(EnvironmentController).Assembly.GetName().Version.ToString();

        public EnvironmentController(ITracer tracer, 
                                     IOperationLock deploymentLock, 
                                     IEnvironment environment)
        {
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _environment = environment;
        }

        [HttpGet]
        public HttpResponseMessage Get()
        {
            // Return the version and other api information (in the end)
            // { 
            //   "version" : "1.0.0"
            // }
            var obj = new JObject(new JProperty("version", _version));
            return Request.CreateResponse(HttpStatusCode.OK, obj);
        }

        [HttpDelete]
        public void Delete()
        {
            // Fail if a deployment is in progress
            if (_deploymentLock.IsHeld)
            {
                HttpResponseMessage response = Request.CreateErrorResponse(HttpStatusCode.Conflict, Resources.Error_DeploymentInProgess);
                throw new HttpResponseException(response);
            }

            using (_tracer.Step("Deleting deployment cache"))
            {
                // Delete the deployment cache
                FileSystemHelpers.DeleteDirectorySafe(_environment.DeploymentCachePath);
            }

            using (_tracer.Step("Deleting repository"))
            {
                // Delete the repository
                FileSystemHelpers.DeleteDirectorySafe(_environment.DeploymentRepositoryPath);
            }
        }
    }
}

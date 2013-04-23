using System;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.SourceControl
{
    public class LiveScmController : ApiController
    {
        private readonly IRepository _repository;
        private readonly IServerConfiguration _serverConfiguration;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IEnvironment _environment;

        public LiveScmController(ITracer tracer,
                                 IOperationLock deploymentLock,
                                 IEnvironment environment,
                                 IRepository repository,
                                 IServerConfiguration serverConfiguration)
        {
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _environment = environment;
            _repository = repository;
            _serverConfiguration = serverConfiguration;
        }

        /// <summary>
        /// Get information about the repository
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpGet]
        public RepositoryInfo GetRepositoryInfo(HttpRequestMessage request)
        {
            var baseUri = new Uri(request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
            return new RepositoryInfo
            {
                Type = _repository.RepositoryType,
                GitUrl = UriHelper.MakeRelative(baseUri, _serverConfiguration.GitServerRoot),
            };
        }

        /// <summary>
        /// Delete the repository
        /// </summary>
        [HttpDelete]
        public void Delete(int deleteWebRoot = 0, int ignoreErrors = 0)
        {
            // Fail if a deployment is in progress
            bool acquired = _deploymentLock.TryLockOperation(() =>
            {
                using (_tracer.Step("Deleting repository"))
                {
                    // Delete the repository
                    FileSystemHelpers.DeleteDirectorySafe(_environment.RepositoryPath, ignoreErrors != 0);
                }

                using (_tracer.Step("Deleting ssh key"))
                {
                    // Delete the ssh key
                    FileSystemHelpers.DeleteDirectorySafe(_environment.SSHKeyPath, ignoreErrors != 0);
                }

                if (deleteWebRoot != 0)
                {
                    using (_tracer.Step("Deleting web root"))
                    {
                        // Delete the wwwroot folder
                        FileSystemHelpers.DeleteDirectoryContentsSafe(_environment.WebRootPath, ignoreErrors != 0);
                    }

                    using (_tracer.Step("Deleting diagnostics"))
                    {
                        // Delete the diagnostic log. This is a slight abuse of deleteWebRoot, but the
                        // real semantic is more to reset the site to a fully clean state
                        FileSystemHelpers.DeleteDirectorySafe(_environment.DiagnosticsPath, ignoreErrors != 0);
                    }
                }

                using (_tracer.Step("Deleting deployment cache"))
                {
                    // Delete the deployment cache
                    FileSystemHelpers.DeleteDirectorySafe(_environment.DeploymentsPath, ignoreErrors != 0);
                }
            }, TimeSpan.Zero);

            if (!acquired)
            {
                HttpResponseMessage response = Request.CreateErrorResponse(HttpStatusCode.Conflict, Resources.Error_DeploymentInProgess);
                throw new HttpResponseException(response);
            }
        }

        /// <summary>
        /// Clean the repository, using 'git clean -xdf'
        /// </summary>
        [HttpPost]
        public void Clean()
        {
            _repository.Clean();
        }
    }
}

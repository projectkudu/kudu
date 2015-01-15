using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services.SourceControl
{
    public class LiveScmController : ApiController
    {
        private readonly IRepository _repository;
        private readonly IServerConfiguration _serverConfiguration;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly IEnvironment _environment;
        private readonly IDeploymentStatusManager _status;

        public LiveScmController(ITracer tracer,
                                 IOperationLock deploymentLock,
                                 IEnvironment environment,
                                 IRepositoryFactory repositoryFactory,
                                 IServerConfiguration serverConfiguration,
                                 IDeploymentStatusManager status)
        {
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _environment = environment;
            _repository = repositoryFactory.GetGitRepository();
            _serverConfiguration = serverConfiguration;
            _status = status;
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
                    string repositoryPath = Path.Combine(_environment.SiteRootPath, Constants.RepositoryPath);
                    if (String.Equals(repositoryPath, _environment.RepositoryPath, StringComparison.OrdinalIgnoreCase))
                    {
                        // Delete the repository
                        FileSystemHelpers.DeleteDirectorySafe(_environment.RepositoryPath, ignoreErrors != 0);
                    }
                    else
                    {
                        // Just delete .git folder
                        FileSystemHelpers.DeleteDirectorySafe(Path.Combine(_environment.RepositoryPath, ".git"), ignoreErrors != 0);

                        FileSystemHelpers.DeleteDirectorySafe(Path.Combine(_environment.RepositoryPath, ".hg"), ignoreErrors != 0);
                    }
                }

                using (_tracer.Step("Delete auto swap lock file"))
                {
                    FileSystemHelpers.DeleteFileSafe(Path.Combine(_environment.LocksPath, AutoSwapHandler.AutoSwapLockFile));
                }

                using (_tracer.Step("Deleting ssh key"))
                {
                    // Delete the ssh key
                    FileSystemHelpers.DeleteDirectorySafe(_environment.SSHKeyPath, ignoreErrors != 0);
                }

                if (deleteWebRoot != 0)
                {
                    // This logic is primarily used to help with site reuse during test.
                    // The flag is not documented for general use.

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

                    using (_tracer.Step("Deleting ASP.NET 5 approot"))
                    {
                        // Delete the approot folder used by ASP.NET 5 apps
                        FileSystemHelpers.DeleteDirectorySafe(Path.Combine(_environment.SiteRootPath, "approot"), ignoreErrors != 0);
                    }

                    // Delete first deployment manifest since it is no longer needed
                    FileSystemHelpers.DeleteFileSafe(Path.Combine(_environment.SiteRootPath, Constants.FirstDeploymentManifestFileName));
                }
                else
                {
                    using (_tracer.Step("Updating initial deployment manifest"))
                    {
                        // The active deployment manifest becomes the baseline initial deployment manifest
                        // When SCM is reconnected, the new deployment will use this manifest to clean the wwwroot
                        SaveInitialDeploymentManifest();
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

        private void SaveInitialDeploymentManifest()
        {
            // Delete any existing one for robustness
            string firstDeploymentManifest = Path.Combine(_environment.SiteRootPath, Constants.FirstDeploymentManifestFileName);
            FileSystemHelpers.DeleteFileSafe(firstDeploymentManifest);

            // Write the new file
            string activeDeploymentId = _status.ActiveDeploymentId;
            if (!String.IsNullOrEmpty(activeDeploymentId))
            {
                string activeDeploymentManifest = Path.Combine(_environment.DeploymentsPath, activeDeploymentId, Constants.ManifestFileName);
                if (FileSystemHelpers.FileExists(activeDeploymentManifest))
                {
                    FileSystemHelpers.CopyFile(activeDeploymentManifest, firstDeploymentManifest, overwrite: true);
                }
            }
        }
    }
}

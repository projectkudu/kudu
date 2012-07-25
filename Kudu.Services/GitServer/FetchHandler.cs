using System;
using System.Linq;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer
{
    public class FetchHandler : IHttpHandler
    {
        private readonly IGitServer _gitServer;
        private readonly IDeploymentManager _deploymentManager;
        private readonly IDeploymentSettingsManager _settings;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;
        private readonly RepositoryConfiguration _configuration;

        public FetchHandler(ITracer tracer,
                            IGitServer gitServer,
                            IDeploymentManager deploymentManager,
                            IDeploymentSettingsManager settings,
                            IOperationLock deploymentLock,
                            RepositoryConfiguration configuration)
        {
            _gitServer = gitServer;
            _deploymentManager = deploymentManager;
            _settings = settings;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _configuration = configuration;
        }

        public bool IsReusable
        {
            get
            {
                return false;
            }
        }

        public void ProcessRequest(HttpContext context)
        {
            using (_tracer.Step("FetchHandler"))
            {
                string json = context.Request.Form["payload"];

                context.Response.TrySkipIisCustomErrors = true;

                if (String.IsNullOrEmpty(json))
                {
                    _tracer.TraceWarning("Received empty json payload");
                    context.Response.StatusCode = 400;
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                RepositoryInfo repositoryInfo = null;

                try
                {
                    repositoryInfo = GetRepositoryInfo(context.Request, json);
                }
                catch (FormatException ex)
                {
                    _tracer.TraceError(ex);
                    context.Response.StatusCode = 400;
                    context.Response.Write(ex.Message);
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                string targetBranch = _settings.GetValue("branch") ?? "master";

                _tracer.Trace("Attempting to fetch target branch {0}", targetBranch);

                if (!targetBranch.Equals(repositoryInfo.Branch, StringComparison.OrdinalIgnoreCase))
                {
                    _tracer.Trace("Expected to fetch {0} but got {1}.", targetBranch, repositoryInfo.Branch);

                    context.Response.StatusCode = 200;
                    context.Response.Write(Resources.NothingToUpdate);
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                _deploymentLock.LockOperation(() =>
                {
                    _gitServer.Initialize(_configuration);
                    _gitServer.SetReceiveInfo(repositoryInfo.OldRef, repositoryInfo.NewRef, repositoryInfo.Branch);
                    _gitServer.FetchWithoutConflict(repositoryInfo.RepositoryUrl, "external", repositoryInfo.Branch);
                    _deploymentManager.Deploy(repositoryInfo.Deployer);
                },
                () =>
                {
                    context.Response.StatusCode = 409;
                    context.ApplicationInstance.CompleteRequest();
                });
            }
        }

        private RepositoryInfo GetRepositoryInfo(HttpRequest request, string json)
        {
            JObject payload = null;
            try
            {
                payload = JObject.Parse(json);
            }
            catch (Exception ex)
            {
                throw new FormatException(Resources.Error_UnsupportedFormat, ex);
            }

            var info = new RepositoryInfo();

            // If it has a repository, then try to get information from that
            var repository = payload.Value<JObject>("repository");

            if (repository != null)
            {
                // Try to assume the github format
                // { repository: { url: "" }, ref: "", before: "", after: "" } 
                info.RepositoryUrl = repository.Value<string>("url");

                // The format of ref is refs/something/something else
                // For master it's normally refs/head/master
                string @ref = payload.Value<string>("ref");

                if (String.IsNullOrEmpty(@ref))
                {
                    throw new FormatException(Resources.Error_UnsupportedFormat);
                }

                // Just get the last token
                info.Branch = @ref.Split('/').Last();
                info.Deployer = GetDeployer(request);
                info.OldRef = payload.Value<string>("before");
                info.NewRef = payload.Value<string>("after");
            }
            else
            {
                // Look for the generic format
                // { url: "", branch: "", deployer: "", oldRef: "", newRef: "" } 
                info.RepositoryUrl = payload.Value<string>("url");
                info.Branch = payload.Value<string>("branch");
                info.Deployer = payload.Value<string>("deployer");
                info.OldRef = payload.Value<string>("oldRef");
                info.NewRef = payload.Value<string>("newRef");
            }

            // If there's no specified branch assume master
            if (String.IsNullOrEmpty(info.Branch))
            {
                // REVIEW: Is this correct
                info.Branch = "master";
            }

            if (String.IsNullOrEmpty(info.RepositoryUrl))
            {
                throw new FormatException(Resources.Error_MissingRepositoryUrl);
            }

            return info;
        }

        private string GetDeployer(HttpRequest httpRequest)
        {
            // This is kind of hacky, we should have a consistent way of figuring out who's pushing to us
            if (httpRequest.Headers["X-Github-Event"] != null)
            {
                return "github";
            }

            // Look for a specific header here
            return null;
        }

        private class RepositoryInfo
        {
            public string RepositoryUrl { get; set; }
            public string OldRef { get; set; }
            public string NewRef { get; set; }
            public string Branch { get; set; }
            public string Deployer { get; set; }
        }
    }
}

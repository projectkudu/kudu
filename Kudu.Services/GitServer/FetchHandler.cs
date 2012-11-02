using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Kudu.Core.SourceControl.Git;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.GitServer
{
    public class FetchHandler : GitServerHttpHandler
    {
        private const string PrivateKeyFile = "id_rsa";
        private const string PublicKeyFile = "id_rsa.pub";

        private readonly IDeploymentSettingsManager _settings;
        private readonly RepositoryConfiguration _configuration;
        private readonly IEnvironment _environment;

        public FetchHandler(ITracer tracer,
                            IGitServer gitServer,
                            IDeploymentManager deploymentManager,
                            IDeploymentSettingsManager settings,
                            IOperationLock deploymentLock,
                            RepositoryConfiguration configuration,
                            IEnvironment environment)
            : base(tracer, gitServer, deploymentLock, deploymentManager)
        {
            _settings = settings;
            _configuration = configuration;
            _environment = environment;
        }

        private string MarkerFilePath
        {
            get
            {
                return Path.Combine(_environment.DeploymentCachePath, "pending");
            }
        }

        public override void ProcessRequest(HttpContext context)
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

                if (_configuration.TraceLevel > 1)
                {
                    TracePayload(json);
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

                _deploymentLock.LockOperation(() =>
                {
                    PerformDeployment(repositoryInfo, targetBranch);
                },
                () =>
                {
                    // Create a marker file that indicates if there's another deployment to pull
                    // because there was a deployment in progress.
                    using (_tracer.Step("Creating pending deployment maker file"))
                    {
                        // REVIEW: This makes the assumption that the repository url is the same.
                        // If it isn't the result would be buggy either way.
                        CreateMarkerFile();
                    }

                    context.Response.StatusCode = 409;
                    context.ApplicationInstance.CompleteRequest();
                });
            }
        }

        private void CreateMarkerFile()
        {
            File.WriteAllText(MarkerFilePath, String.Empty);
        }

        private bool MarkerFileExists()
        {
            return File.Exists(MarkerFilePath);
        }

        private bool DeleteMarkerFile()
        {
            return FileSystemHelpers.DeleteFileSafe(MarkerFilePath);
        }

        private void PerformDeployment(RepositoryInfo repositoryInfo, string targetBranch)
        {
            if (repositoryInfo.UseSSH)
            {
                using (_tracer.Step("Prepare SSH environment"))
                {
                    _gitServer.SetSSHEnv(repositoryInfo.Host, _environment.SiteRootPath);
                }
            }

            bool hasPendingDeployment;

            do
            {
                hasPendingDeployment = false;

                using (_tracer.Step("Performing fetch based deployment"))
                {
                    using (_deploymentManager.CreateTemporaryDeployment(Resources.FetchingChanges))
                    {
                        // Configure the repository
                        _gitServer.Initialize(_configuration);

                        // Setup the receive info (this is important to know if branches were deleted etc)
                        _gitServer.SetReceiveInfo(repositoryInfo.OldRef, repositoryInfo.NewRef, targetBranch);

                        // Fetch from url
                        _gitServer.FetchWithoutConflict(repositoryInfo.RepositoryUrl, "external", targetBranch);

                        // Perform the actual deployment
                        _deploymentManager.Deploy(repositoryInfo.Deployer);

                        if (MarkerFileExists())
                        {
                            _tracer.Trace("Pending deployment marker file exists");

                            hasPendingDeployment = DeleteMarkerFile();

                            if (hasPendingDeployment)
                            {
                                _tracer.Trace("Deleted marker file");
                            }
                            else
                            {
                                _tracer.TraceError("Failed to delete marker file");
                            }
                        }
                    }
                }

            } while (hasPendingDeployment);
        }

        private void TracePayload(string json)
        {
            var attribs = new Dictionary<string, string>
            {
                { "json", json }
            };

            _tracer.Trace("payload", attribs);
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
                if (request.UserAgent != null && request.UserAgent.StartsWith("Bitbucket", StringComparison.OrdinalIgnoreCase))
                {
                    // bitbucket format
                    // { repository: { absolute_url: "/a/b", is_private: true }, canon_url: "https//..." } 
                    string server = payload.Value<string>("canon_url");     // e.g. https://bitbucket.org
                    string path = repository.Value<string>("absolute_url"); // e.g. /davidebbo/testrepo/

                    // Combine them to get the full URL
                    info.RepositoryUrl = server + path;

                    info.IsPrivate = repository.Value<bool>("is_private");

                    info.Deployer = "Bitbucket";

                    // We don't get any refs from bitbucket, so write dummy string (we ignore it later anyway)
                    info.OldRef = "dummy";

                    // When there are no commits, set the new ref to an all-zero string to cause the logic in
                    // GitDeploymentRepository.GetReceiveInfo ignore the push
                    var commits = payload.Value<JArray>("commits");
                    info.NewRef = commits.Count == 0 ? "000" : "dummy";
                }
                else
                {
                    // github format
                    // { repository: { url: "https//...", private: False }, ref: "", before: "", after: "" } 
                    info.RepositoryUrl = repository.Value<string>("url");

                    info.IsPrivate = repository.Value<bool>("private");

                    // The format of ref is refs/something/something else
                    // For master it's normally refs/head/master
                    string @ref = payload.Value<string>("ref");

                    if (String.IsNullOrEmpty(@ref))
                    {
                        throw new FormatException(Resources.Error_UnsupportedFormat);
                    }

                    // Just get the last token
                    info.Deployer = GetDeployer(request);
                    info.OldRef = payload.Value<string>("before");
                    info.NewRef = payload.Value<string>("after");
                }

                // private repo, use SSH
                if (info.IsPrivate)
                {
                    Uri uri = new Uri(info.RepositoryUrl);
                    if (uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        info.Host = "git@" + uri.Host;
                        info.RepositoryUrl = info.Host + ":" + uri.AbsolutePath.TrimStart('/');
                        info.UseSSH = true;
                    }
                }
            }
            else
            {
                // Look for the generic format
                // { url: "", branch: "", deployer: "", oldRef: "", newRef: "" } 
                info.RepositoryUrl = payload.Value<string>("url");
                info.Deployer = payload.Value<string>("deployer");
                info.OldRef = payload.Value<string>("oldRef");
                info.NewRef = payload.Value<string>("newRef");
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
                return "GitHub";
            }

            // Look for a specific header here
            return null;
        }

        private class RepositoryInfo
        {
            public string RepositoryUrl { get; set; }
            public bool IsPrivate { get; set; }
            public bool UseSSH { get; set; }
            public string Host { get; set; }
            public string OldRef { get; set; }
            public string NewRef { get; set; }
            public string Deployer { get; set; }
        }
    }
}

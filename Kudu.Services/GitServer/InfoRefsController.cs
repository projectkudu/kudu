#region License

// Copyright 2010 Jeremy Skinner (http://www.jeremyskinner.co.uk)
//  
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
// http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
// 
// The latest version of this file can be found at http://github.com/JeremySkinner/git-dot-aspx

// This file was modified from the one found in git-dot-aspx

#endregion
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.GitServer
{    
    public class InfoRefsController : ApiController
    {
        private readonly IDeploymentManager _deploymentManager;
        private readonly IGitServer _gitServer;
        private readonly ITracer _tracer;
        private readonly IDeploymentSettingsManager _settings;
        private readonly string _webRootPath;
        private readonly IRepositoryFactory _repositoryFactory;

        public InfoRefsController(
            ITracer tracer,
            IGitServer gitServer,
            IDeploymentManager deploymentManager,
            IDeploymentSettingsManager settings,
            IEnvironment environment,
            IRepositoryFactory repositoryFactory)
        {
            _gitServer = gitServer;
            _deploymentManager = deploymentManager;
            _tracer = tracer;
            _settings = settings;
            _webRootPath = environment.WebRootPath;
            _repositoryFactory = repositoryFactory;
        }

        [HttpGet]
        public HttpResponseMessage Execute(string service)
        {
            using (_tracer.Step("InfoRefsService.Execute"))
            {
                if (!_settings.IsScmEnabled())
                {
                    return Request.CreateErrorResponse(HttpStatusCode.Forbidden, Resources.Error_GitIsDisabled);
                }

                // Ensure that the target directory does not have a non-Git repository.
                _repositoryFactory.EnsureRepository(RepositoryType.Git);

                service = GetServiceType(service);
                bool isUsingSmartProtocol = service != null;

                // Service has been specified - we're working with the smart protocol
                if (isUsingSmartProtocol)
                {
                    return SmartInfoRefs(service);
                }

                // Dumb protocol isn't supported
                _tracer.TraceWarning("Attempting to use dumb protocol.");
                return Request.CreateErrorResponse(HttpStatusCode.NotImplemented, Resources.Error_DumbProtocolNotSupported);
            }
        }

        private HttpResponseMessage SmartInfoRefs(string service)
        {
            using (_tracer.Step("InfoRefsService.SmartInfoRefs"))
            {
                var memoryStream = new MemoryStream();

                memoryStream.PktWrite("# service=git-{0}\n", service);
                memoryStream.PktFlush();

                if (service == "upload-pack")
                {
                    //// Initialize the repository from the deployment files (if this is the first commit)
                    //ChangeSet changeSet = _gitServer.Initialize(_configuration, _webRootPath);
                    //_gitServer.AdvertiseUploadPack(memoryStream);

                    //// If we just created the repo, make a 'pseudo' deployment for the initial commit
                    //if (changeSet != null)
                    //{
                    //    _deploymentManager.CreateExistingDeployment(changeSet.Id, _configuration.Username);
                    //}

                    _gitServer.Initialize();
                    _gitServer.AdvertiseUploadPack(memoryStream);
                }
                else if (service == "receive-pack")
                {
                    _gitServer.Initialize();
                    _gitServer.AdvertiseReceivePack(memoryStream);
                }

                _tracer.Trace("Writing {0} bytes", memoryStream.Length);

                HttpContent content = memoryStream.AsContent();

                content.Headers.ContentType =
                    new MediaTypeHeaderValue("application/x-git-{0}-advertisement".With(service));
                // Explicitly set the charset to empty string
                // We do this as certain git clients (jgit) require it to be empty.
                // If we don't set it, then it defaults to utf-8, which breaks jgit's logic for detecting smart http
                content.Headers.ContentType.CharSet = "";

                var responseMessage = new HttpResponseMessage();
                responseMessage.Content = content;
                responseMessage.WriteNoCache();
                return responseMessage;
            }
        }

        protected string GetServiceType(string service)
        {
            if (String.IsNullOrWhiteSpace(service))
            {
                return null;
            }

            return service.Replace("git-", "");
        }
    }
}

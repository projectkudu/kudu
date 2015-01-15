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
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Web.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.GitServer
{    
    public class InfoRefsController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly IRepositoryFactory _repositoryFactory;
        private readonly Func<Type, object> _getInstance;

        // Delay ninject binding
        public InfoRefsController(Func<Type, object> getInstance)
        {
            _getInstance = getInstance;
            _repositoryFactory = GetInstance<IRepositoryFactory>();
            _tracer = GetInstance<ITracer>();
        }

        [HttpGet]
        public HttpResponseMessage Execute(string service = null)
        {
            using (_tracer.Step("InfoRefsService.Execute"))
            {
                // Ensure that the target directory does not have a non-Git repository.
                IRepository repository = _repositoryFactory.GetRepository();
                if (repository != null && repository.RepositoryType != RepositoryType.Git)
                {
                    return Request.CreateErrorResponse(HttpStatusCode.BadRequest, String.Format(CultureInfo.CurrentCulture, Resources.Error_NonGitRepositoryFound, repository.RepositoryType));
                }

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
                using (var memoryStream = new MemoryStream())
                {
                    memoryStream.PktWrite("# service=git-{0}\n", service);
                    memoryStream.PktFlush();

                    if (service == "upload-pack")
                    {
                        InitialCommitIfNecessary();

                        var gitServer = GetInstance<IGitServer>();
                        gitServer.AdvertiseUploadPack(memoryStream);
                    }
                    else if (service == "receive-pack")
                    {
                        var gitServer = GetInstance<IGitServer>();
                        gitServer.AdvertiseReceivePack(memoryStream);
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
        }

        protected static string GetServiceType(string service)
        {
            if (String.IsNullOrWhiteSpace(service))
            {
                return null;
            }

            return service.Replace("git-", "");
        }

        private T GetInstance<T>()
        {
            return (T)_getInstance(typeof(T));
        }

        public void InitialCommitIfNecessary()
        {
            var settings = GetInstance<IDeploymentSettingsManager>();

            // only support if LocalGit
            if (settings.GetValue(SettingsKeys.ScmType) != ScmType.LocalGit)
            {
                return;
            }

            // get repository for the WebRoot
            var initLock = GetInstance<IOperationLock>();
            initLock.LockOperation(() =>
            {
                IRepository repository = _repositoryFactory.GetRepository();

                // if repository exists, no needs to do anything
                if (repository != null)
                {
                    return;
                }

                var repositoryPath = settings.GetValue(SettingsKeys.RepositoryPath);

                // if repository settings is defined, it's already been taken care.
                if (!String.IsNullOrEmpty(repositoryPath))
                {
                    return;
                }

                var env = GetInstance<IEnvironment>();
                // it is default webroot content, do nothing
                if (DeploymentHelper.IsDefaultWebRootContent(env.WebRootPath))
                {
                    return;
                }

                // Set repo path to WebRoot
                var previous = env.RepositoryPath;
                env.RepositoryPath = Path.Combine(env.SiteRootPath, Constants.WebRoot);

                repository = _repositoryFactory.GetRepository();
                if (repository != null)
                {
                    env.RepositoryPath = previous;
                    return;
                }

                // do initial commit
                repository = _repositoryFactory.EnsureRepository(RepositoryType.Git);

                // Once repo is init, persist the new repo path
                settings.SetValue(SettingsKeys.RepositoryPath, Constants.WebRoot);

                repository.Commit("Initial Commit", authorName: null, emailAddress: null);

            }, GitExeServer.InitTimeout);
        }
    }
}

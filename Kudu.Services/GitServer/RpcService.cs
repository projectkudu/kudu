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

namespace Kudu.Services.GitServer
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.ServiceModel;
    using System.ServiceModel.Web;
    using System.Text;
    using System.Threading;
    using System.Web.Hosting;
    using Kudu.Contracts.Infrastructure;
    using Kudu.Contracts.Tracing;
    using Kudu.Core.Deployment;
    using Kudu.Core.SourceControl.Git;
    using Kudu.Services.Infrastructure;

    // Handles {project}/git-upload-pack and {project}/git-receive-pack
    [ServiceContract]
    public class RpcService
    {
        private readonly IDeploymentManagerFactory _deploymentManagerFactory;
        private readonly IGitServer _gitServer;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;

        public RpcService(ITracer tracer,
                          IGitServer gitServer,
                          IDeploymentManagerFactory deploymentManagerFactory,
                          IOperationLock deploymentLock)
        {
            _gitServer = gitServer;
            _deploymentManagerFactory = deploymentManagerFactory;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
        }

        [Description("Handles a 'git pull' command.")]
        [WebInvoke(UriTemplate = "git-upload-pack")]
        public HttpResponseMessage UploadPack(HttpRequestMessage request)
        {
            using (_tracer.Step("RpcService.UploadPack"))
            {
                var memoryStream = new MemoryStream();

                _gitServer.Upload(GetInputStream(request), memoryStream);

                return CreateResponse(memoryStream, "application/x-git-{0}-result".With("upload-pack"));
            }
        }

        [Description("Handles a 'git push' command.")]
        [WebInvoke(UriTemplate = "git-receive-pack")]
        public HttpResponseMessage ReceivePack(HttpRequestMessage request)
        {
            using (_tracer.Step("RpcService.ReceivePack"))
            {
                bool lockTaken = _deploymentLock.Lock();

                if (!lockTaken)
                {
                    return new HttpResponseMessage(HttpStatusCode.Conflict);
                }

                var memoryStream = new MemoryStream();

                // Only if we've completed the receive pack should we start a deployment
                if (_gitServer.Receive(GetInputStream(request), memoryStream))
                {
                    Deploy();
                }
                else
                {
                    _deploymentLock.Release();
                }

                return CreateResponse(memoryStream, "application/x-git-{0}-result".With("receive-pack"));
            }
        }

        private void Deploy()
        {
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { }
                finally
                {
                    // Avoid thread aborts by putting this logic in the finally
                    var deployer = new Deployer(_tracer, _deploymentManagerFactory, _deploymentLock);
                    deployer.Deploy();
                }
            });
        }

        private Stream GetInputStream(HttpRequestMessage request)
        {
            using (_tracer.Step("RpcService.GetInputStream"))
            {
                if (request.Content.Headers.ContentEncoding.Contains("gzip"))
                {
                    return new GZipStream(request.Content.ReadAsStreamAsync().Result, CompressionMode.Decompress);
                }

                return request.Content.ReadAsStreamAsync().Result;
            }
        }

        private HttpResponseMessage CreateResponse(MemoryStream stream, string mediaType)
        {
            _tracer.Trace("Writing {0} bytes", stream.Length);

            // TODO: Should we only do this in debug mode?
            _tracer.Trace("Git stream", new Dictionary<string, string>
            {
                { "type", "gitStream" },
                { "output", Encoding.UTF8.GetString(stream.ToArray()) }
            });

            HttpContent content = stream.AsContent();

            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            // REVIEW: Why is it that we do not write an empty Content-Type here, like for InfoRefsController?

            var response = new HttpResponseMessage();
            response.Content = content;
            response.WriteNoCache();
            return response;
        }

        /// <summary>
        /// Let ASP.NET know about our background deployment thread.
        /// </summary>
        private class Deployer : IRegisteredObject
        {
            private readonly ITracer _tracer;
            private readonly IDeploymentManagerFactory _deploymentManagerFactory;
            private readonly IOperationLock _deploymentLock;

            public Deployer(ITracer tracer,
                            IDeploymentManagerFactory deploymentManagerFactory,
                            IOperationLock deploymentLock)
            {
                _tracer = tracer;
                _deploymentManagerFactory = deploymentManagerFactory;
                _deploymentLock = deploymentLock;

                // Let the hosting environment know about this object.
                HostingEnvironment.RegisterObject(this);
            }

            public void Stop(bool immediate)
            {
                if (!_deploymentLock.IsHeld)
                {
                    return;
                }

                _tracer.TraceWarning("Initiating ASP.NET shutdown. Waiting on deployment to complete.");

                // Wait until ASP.NET or IIS kills us
                bool timeout = _deploymentLock.Wait(TimeSpan.MaxValue);

                if (timeout)
                {
                    _tracer.TraceWarning("Deployment timed out.");
                }
                else
                {
                    _tracer.Trace("Deployment completed.");
                }
            }

            public void Deploy()
            {
                try
                {
                    IDeploymentManager deploymentManager = _deploymentManagerFactory.CreateDeploymentManager();
                    deploymentManager.Deploy();
                }
                catch (Exception ex)
                {
                    _tracer.TraceError(ex);
                }
                finally
                {
                    _deploymentLock.Release();
                    HostingEnvironment.UnregisterObject(this);
                }
            }
        }
    }
}

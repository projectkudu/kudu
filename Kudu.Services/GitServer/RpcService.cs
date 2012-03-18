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
    using System.Threading;
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

        public RpcService(ITracer tracer, IGitServer gitServer, IDeploymentManagerFactory deploymentManagerFactory, IOperationLock deploymentLock)
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
            var attribs = new Dictionary<string, string> { 
                { "length", stream.Length.ToString() },
                { "type", "response" }
            };

            HttpContent content = null;
            using (_tracer.Step("Response content", attribs))
            {
                content = stream.AsContent();
            }

            content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
            // REVIEW: Why is it that we do not write an empty Content-Type here, like for InfoRefsController?

            var response = new HttpResponseMessage();
            response.Content = content;
            response.WriteNoCache();
            return response;
        }
    }
}

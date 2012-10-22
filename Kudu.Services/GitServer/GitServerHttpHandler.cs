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

using System.IO;
using System.IO.Compression;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Deployment;

namespace Kudu.Services.GitServer
{
    public abstract class GitServerHttpHandler : IHttpHandler
    {
        protected readonly IGitServer _gitServer;
        protected readonly ITracer _tracer;
        protected readonly IOperationLock _deploymentLock;
        protected readonly IDeploymentManager _deploymentManager;

        public GitServerHttpHandler(ITracer tracer, IGitServer gitServer, IOperationLock deploymentLock, IDeploymentManager deploymentManager)
        {
            _gitServer = gitServer;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
            _deploymentManager = deploymentManager;
        }

        public virtual bool IsReusable
        {
            get
            {
                return false;
            }
        }

        public abstract void ProcessRequest(HttpContext context);

        protected void UpdateNoCacheForResponse(HttpResponse response)
        {
            response.Buffer = false;
            response.BufferOutput = false;

            response.AddHeader("Expires", "Fri, 01 Jan 1980 00:00:00 GMT");
            response.AddHeader("Pragma", "no-cache");
            response.AddHeader("Cache-Control", "no-cache, max-age=0, must-revalidate");
        }

        protected Stream GetInputStream(HttpRequest request)
        {
            using (_tracer.Step("RpcService.GetInputStream"))
            {
                var contentEncoding = request.Headers["Content-Encoding"];

                if (contentEncoding != null && contentEncoding.Contains("gzip"))
                {
                    return new GZipStream(request.InputStream, CompressionMode.Decompress);
                }

                return request.InputStream;
            }
        }
    }
}

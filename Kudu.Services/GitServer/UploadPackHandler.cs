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

namespace Kudu.Services.GitServer
{
    public class UploadPackHandler : IHttpHandler
    {
        private readonly IGitServer _gitServer;
        private readonly ITracer _tracer;
        private readonly IOperationLock _deploymentLock;

        public UploadPackHandler(ITracer tracer,
                                  IGitServer gitServer,
                                  IOperationLock deploymentLock)
        {
            _gitServer = gitServer;
            _tracer = tracer;
            _deploymentLock = deploymentLock;
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
            using (_tracer.Step("RpcService.UploadPackHandler"))
            {
                context.Response.Buffer = false;
                context.Response.BufferOutput = false;

                context.Response.ContentType = "application/x-git-{0}-result".With("upload-pack");
                context.Response.AddHeader("Expires", "Fri, 01 Jan 1980 00:00:00 GMT");
                context.Response.AddHeader("Pragma", "no-cache");
                context.Response.AddHeader("Cache-Control", "no-cache, max-age=0, must-revalidate");

                _gitServer.Upload(GetInputStream(context.Request), context.Response.OutputStream);
            }
        }

        private Stream GetInputStream(HttpRequest request)
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

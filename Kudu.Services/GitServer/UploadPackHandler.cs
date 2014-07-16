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

using System.Net;
using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;

namespace Kudu.Services.GitServer
{
    public class UploadPackHandler : GitServerHttpHandler
    {
        public UploadPackHandler(ITracer tracer,
                                  IGitServer gitServer,
                                  IOperationLock deploymentLock,
                                  IDeploymentManager deploymentManager)
            : base(tracer, gitServer, deploymentLock, deploymentManager)
        {
        }

        public override void ProcessRequestBase(HttpContextBase context)
        {
            using (Tracer.Step("RpcService.UploadPackHandler"))
            {
                UpdateNoCacheForResponse(context.Response);

                context.Response.ContentType = "application/x-git-{0}-result".With("upload-pack");

                GitServer.Upload(context.Request.GetInputStream(), context.Response.OutputStream);
            }
        }
    }
}

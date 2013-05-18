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

using System.Web;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl.Git;

namespace Kudu.Services.GitServer
{
    public abstract class GitServerHttpHandler : IHttpHandler
    {
        protected IGitServer GitServer { get; private set; }
        protected ITracer Tracer { get; private set; }
        protected IOperationLock DeploymentLock { get; private set; }
        protected IDeploymentManager DeploymentManager { get; private set; }

        protected GitServerHttpHandler(ITracer tracer, 
                                    IGitServer gitServer, 
                                    IOperationLock deploymentLock, 
                                    IDeploymentManager deploymentManager)
        {
            GitServer = gitServer;
            Tracer = tracer;
            DeploymentLock = deploymentLock;
            DeploymentManager = deploymentManager;
        }

        public virtual bool IsReusable
        {
            get
            {
                return false;
            }
        }

        public void ProcessRequest(HttpContext context)
        {
            ProcessRequestBase(new HttpContextWrapper(context));
        }

        public abstract void ProcessRequestBase(HttpContextBase context);

        public static void UpdateNoCacheForResponse(HttpResponseBase response)
        {
            response.Buffer = false;
            response.BufferOutput = false;

            response.AddHeader("Expires", "Fri, 01 Jan 1980 00:00:00 GMT");
            response.AddHeader("Pragma", "no-cache");
            response.AddHeader("Cache-Control", "no-cache, max-age=0, must-revalidate");
        }
    }
}

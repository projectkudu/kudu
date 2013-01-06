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
using Kudu.Services.Infrastructure;

namespace Kudu.Services.GitServer
{
    public class ReceivePackHandler : GitServerHttpHandler
    {
        public ReceivePackHandler(ITracer tracer,
                                  IGitServer gitServer,
                                  IOperationLock deploymentLock,
                                  IDeploymentManager deploymentManager,
                                  IDeploymentSettingsManager settings)
            : base(tracer, gitServer, deploymentLock, deploymentManager, settings)
        {
        }

        public override void ProcessRequest(HttpContext context)
        {
            using (_tracer.Step("RpcService.ReceivePack"))
            {
                if (!_settings.IsGitEnabled())
                {
                    context.Response.StatusCode = (int)HttpStatusCode.Forbidden;
                    context.ApplicationInstance.CompleteRequest();
                    return;
                }

                _deploymentLock.LockOperation(() =>
                {
                    string username = null;
                    if (AuthUtility.TryExtractBasicAuthUser(context.Request, out username))
                    {
                        _gitServer.SetDeployer(username);
                    }

                    UpdateNoCacheForResponse(context.Response);

                    context.Response.ContentType = "application/x-git-receive-pack-result";
                    
                    using (_deploymentManager.CreateTemporaryDeployment(Resources.ReceivingChanges))
                    {
                        _gitServer.Receive(context.Request.GetInputStream(), context.Response.OutputStream);
                    }
                },
                () =>
                {
                    context.Response.StatusCode = 409;
                    context.ApplicationInstance.CompleteRequest();
                });
            }
        }
    }
}

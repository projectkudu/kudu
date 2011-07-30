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

namespace Kudu.Services.GitServer {
    using System;
    using System.IO;
    using System.IO.Compression;
    using System.Threading;
    using System.Web.Mvc;
    using System.Web.SessionState;
    using Kudu.Core.Deployment;
    using Kudu.Core.SourceControl.Git;
    using Kudu.Services.Authorization;

    // Handles project/git-upload-pack and project/git-receive-pack
    [SessionState(SessionStateBehavior.Disabled)]
    [BasicAuthorize]
    public class RpcController : Controller {
        private readonly IDeploymentManager _deploymentManager;
        private readonly IGitServer _gitServer;

        public RpcController(IGitServer gitServer,
                             IDeploymentManager deploymentManager) {
            _gitServer = gitServer;
            _deploymentManager = deploymentManager;
        }

        [HttpPost]
        public void UploadPack() {
            ExecuteRpc("upload-pack", () => {
                _gitServer.Upload(GetInputStream(), Response.OutputStream);
            });
        }

        [HttpPost]
        public void ReceivePack() {
            ExecuteRpc("receive-pack", () => {
                _gitServer.Receive(GetInputStream(), Response.OutputStream);
            });

            ThreadPool.QueueUserWorkItem(_ => {
                try {
                    _deploymentManager.Deploy();
                }
                catch (Exception) {
                    // TODO: Log something
                }
            });
        }

        private Stream GetInputStream() {
            if (Request.Headers["Content-Encoding"] == "gzip") {
                return new GZipStream(Request.InputStream, CompressionMode.Decompress);
            }
            return Request.InputStream;
        }

        private void ExecuteRpc(string rpc, Action action) {
            Response.ContentType = "application/x-git-{0}-result".With(rpc);
            Response.WriteNoCache();

            action();
        }
    }
}

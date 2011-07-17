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
    using System.Linq;
    using System.Threading;
    using System.Web.Mvc;
    using System.Web.SessionState;
    using ICSharpCode.SharpZipLib.GZip;
    using Kudu.Core.Deployment;
    using Kudu.Core.SourceControl;

    // Handles project/git-upload-pack and project/git-receive-pack
    [SessionState(SessionStateBehavior.Disabled)]
    public class RpcController : Controller {
        private readonly Repository _repository;
        private readonly IDeployerFactory _deployerFactory;
        private readonly IRepository _deployRepository;

        public RpcController(Repository repository,
                             IRepositoryManager repositoryManager,
                             IDeployerFactory deployerFactory) {
            _repository = repository;
            _deployRepository = repositoryManager.GetRepository();
            _deployerFactory = deployerFactory;
        }

        [HttpPost]
        public void UploadPack(string project) {
            ExecuteRpc("upload-pack", () => {
                _repository.Upload(GetInputStream(), Response.OutputStream);
            });
        }

        [HttpPost]
        public void ReceivePack(string project) {
            ExecuteRpc("receive-pack", () => {
                _repository.Receive(GetInputStream(), Response.OutputStream);
            });

            // Queue a build (TODO: Report information about the build we're queuing)
            ThreadPool.QueueUserWorkItem(_ => Deploy());
        }

        private Stream GetInputStream() {
            if (Request.Headers["Content-Encoding"] == "gzip") {
                return new GZipInputStream(Request.InputStream);
            }
            return Request.InputStream;
        }

        private void ExecuteRpc(string rpc, Action action) {
            Response.ContentType = "application/x-git-{0}-result".With(rpc);
            Response.WriteNoCache();

            action();
        }

        private void Deploy() {
            var activeBranch = _deployRepository.GetBranches().FirstOrDefault(b => b.Active);
            string id = _deployRepository.CurrentId;

            if (activeBranch != null) {
                _deployRepository.Update(activeBranch.Name);
            }
            else {
                _deployRepository.Update(id);
            }

            IDeployer deployer = _deployerFactory.CreateDeployer();
            deployer.Deploy(id);
        }
    }
}

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
    using System.ComponentModel;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.ServiceModel;
    using System.ServiceModel.Web;
    using Kudu.Contracts;
    using Kudu.Core.SourceControl.Git;

    // Handles /{project}/info/refs
    [ServiceContract]
    public class InfoRefsService
    {
        private readonly IGitServer _gitServer;
        private readonly IProfiler _profiler;

        public InfoRefsService(IProfiler profiler, IGitServer gitServer)
        {
            _gitServer = gitServer;
            _profiler = profiler;
        }

        [Description("Handles git commands.")]
        [WebGet(UriTemplate = "?service={service}")]
        public HttpResponseMessage Execute(string service)
        {
            using (_profiler.Step("InfoRefsService.Execute"))
            {
                service = GetServiceType(service);
                bool isUsingSmartProtocol = service != null;

                // Service has been specified - we're working with the smart protocol
                if (isUsingSmartProtocol)
                {
                    return SmartInfoRefs(service);
                }

                throw new Exception("Dumb protocol not supported"); // REVIEW: Consider throw 501 Not Implemented
            }
        }

        private HttpResponseMessage SmartInfoRefs(string service)
        {
            using (_profiler.Step("InfoRefsService.SmartInfoRefs"))
            {
                var memoryStream = new MemoryStream();

                memoryStream.PktWrite("# service=git-{0}\n", service);
                memoryStream.PktFlush();

                if (service == "upload-pack")
                {
                    _gitServer.Initialize();
                    _gitServer.AdvertiseUploadPack(memoryStream);
                }

                else if (service == "receive-pack")
                {
                    _gitServer.Initialize();
                    _gitServer.AdvertiseReceivePack(memoryStream);
                }

                string flushStepTitle = String.Format("Flushing stream. P: {0}, L: {1}", memoryStream.Position, memoryStream.Length);
                using (_profiler.Step(flushStepTitle))
                {
                    memoryStream.Flush();
                    memoryStream.Position = 0;
                }

                var content = new StreamContent(memoryStream);
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

        protected string GetServiceType(string service)
        {
            if (String.IsNullOrWhiteSpace(service))
            {
                return null;
            }

            return service.Replace("git-", "");
        }
    }
}

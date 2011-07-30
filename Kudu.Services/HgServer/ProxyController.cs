using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Web;
using System.Web.Mvc;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.HgServer {
    public class ProxyController : Controller {
        private readonly IHgServer _hgServer;
        private readonly IServerConfiguration _configuration;
        private readonly IDeploymentManager _deploymentManager;

        public ProxyController(IHgServer hgServer, 
                               IServerConfiguration configuration,
                               IDeploymentManager deploymentManager) {
            _hgServer = hgServer;
            _configuration = configuration;
            _deploymentManager = deploymentManager;
        }

        public ActionResult ProxyRequest() {
            string hgRoot = HttpRuntime.AppDomainAppVirtualPath + "/" + _configuration.HgServerRoot;

            if (!Request.RawUrl.StartsWith(hgRoot, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException();
            }

            string pathToProxy = Request.RawUrl.Substring(hgRoot.Length);

            if (!_hgServer.IsRunning) {
                _hgServer.Start();
            }

            var uri = new Uri(_hgServer.Url + pathToProxy);

            var proxyRequest = (HttpWebRequest)WebRequest.Create(uri);

            proxyRequest.Method = Request.HttpMethod;

            foreach (string headerName in Request.Headers) {
                string headerValue = Request.Headers[headerName];

                if (headerName == "Accept") {
                    proxyRequest.Accept = headerValue;
                }
                else if (headerName == "Host") {
                    proxyRequest.Host = headerValue;
                }
                else if (headerName == "User-Agent") {
                    proxyRequest.UserAgent = headerValue;
                }
                else if (headerName == "Content-Length") {
                    proxyRequest.ContentLength = long.Parse(headerValue);
                }
                else if (headerName == "Content-Type") {
                    proxyRequest.ContentType = headerValue;
                }
                else if (headerName == "Connection") {
                    // This blows up with "Keep-Alive and Close may not be set using this property"
                    //proxyRequest.Connection = headerValue;
                }
                else {
                    proxyRequest.Headers[headerName] = headerValue;
                }
            }

            if (Request.ContentLength > 0) {
                Request.InputStream.CopyTo(proxyRequest.GetRequestStream());
            }

            using (var proxyResponse = (HttpWebResponse)proxyRequest.GetResponse()) {

                foreach (string headerName in proxyResponse.Headers) {
                    string headerValue = proxyResponse.Headers[headerName];
                    Response.AddHeader(headerName, headerValue);
                }

                using (Stream proxyResponseStream = proxyResponse.GetResponseStream()) {
                    proxyResponseStream.CopyTo(Response.OutputStream);
                }
            }

            // After we run the unbundle command we can start the deployment
            string cmd = Request["cmd"];
            if (String.Equals(cmd, "unbundle", StringComparison.OrdinalIgnoreCase)) {
                ThreadPool.QueueUserWorkItem(_ => {
                    try {
                        _deploymentManager.Deploy();
                    }
                    catch (Exception ex) {
                        Debug.WriteLine("Error deploying");
                        Debug.WriteLine(ex.Message);
                    }
                });
            }

            return new EmptyResult();
        }
    }
}

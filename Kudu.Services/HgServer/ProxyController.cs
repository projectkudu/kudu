using System;
using System.IO;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.HgServer {
    public class ProxyController : Controller {
        private readonly IServer _server;
        private readonly IServerConfiguration _configuration;

        public ProxyController(IServer server, 
                               IServerConfiguration configuration) {
            _server = server;
            _configuration = configuration;
        }

        public ActionResult ProxyRequest() {
            string hgRoot = HttpRuntime.AppDomainAppVirtualPath + "/" + _configuration.HgServerRoot;

            if (!Request.RawUrl.StartsWith(hgRoot, StringComparison.OrdinalIgnoreCase)) {
                throw new ArgumentException();
            }

            string pathToProxy = Request.RawUrl.Substring(hgRoot.Length);

            if (!_server.IsRunning) {
                _server.Start();
            }

            var uri = new Uri(_server.Url + pathToProxy);

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

            return new EmptyResult();
        }
    }
}

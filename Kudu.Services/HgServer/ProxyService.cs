using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Threading;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Hg;

namespace Kudu.Services.HgServer
{
    [ServiceContract]
    public class ProxyService
    {
        private readonly IHgServer _hgServer;
        private readonly IServerConfiguration _configuration;
        private readonly IDeploymentManager _deploymentManager;
        private readonly IServerRepository _severRepository;

        public ProxyService(IHgServer hgServer,
                            IServerConfiguration configuration,
                            IDeploymentManager deploymentManager,
                            IServerRepository severRepository)
        {
            _hgServer = hgServer;
            _configuration = configuration;
            _deploymentManager = deploymentManager;
            _severRepository = severRepository;
        }

        [Description("Handles raw Mercurial HTTP service requests using POST.")]
        [WebInvoke(UriTemplate = "")]
        public HttpResponseMessage PostProxy(HttpRequestMessage request)
        {
            return ProxyRequest(request);
        }

        [Description("Handles raw Mercurial HTTP service requests using GET.")]
        [WebGet(UriTemplate = "")]
        public HttpResponseMessage GetProxy(HttpRequestMessage request)
        {
            return ProxyRequest(request);
        }

        private HttpResponseMessage ProxyRequest(HttpRequestMessage request)
        {
            string hgRoot = _configuration.ApplicationName + "/" + _configuration.HgServerRoot;

            if (!hgRoot.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                hgRoot = "/" + hgRoot;
            }

            if (!request.RequestUri.PathAndQuery.StartsWith(hgRoot, StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException();
            }

            string pathToProxy = request.RequestUri.PathAndQuery.Substring(hgRoot.Length);

            if (!_hgServer.IsRunning)
            {
                _severRepository.Initialize(null);
                _hgServer.Start();
            }

            var uri = new Uri(_hgServer.Url + pathToProxy);

            HttpClient client = new HttpClient();
            client.DefaultRequestHeaders.Clear();
            foreach (KeyValuePair<string, IEnumerable<string>> header in request.Headers)
            {
                client.DefaultRequestHeaders.Add(header.Key, header.Value);
            }

            HttpResponseMessage proxyResponse;
            switch (request.Method.Method.ToUpperInvariant())
            {
                case "GET":
                    proxyResponse = client.GetAsync(uri).Result;
                    break;
                case "POST":
                    proxyResponse = client.PostAsync(uri, request.Content).Result;
                    break;
                default:
                    return new HttpResponseMessage(System.Net.HttpStatusCode.MethodNotAllowed);
            }

            if (request.RequestUri.Query.Contains("cmd=unbundle"))
            {
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        _deploymentManager.Deploy();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine("Error deploying");
                        Debug.WriteLine(ex.Message);
                    }
                });
            }

            return proxyResponse;
        }
    }
}

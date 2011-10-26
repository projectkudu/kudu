using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.ServiceModel;
using System.ServiceModel.Web;
using System.Threading;
using Kudu.Core.Deployment;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Hg;
using Kudu.Services.Infrastructure;

namespace Kudu.Services.HgServer
{
    [ServiceContract]
    public class ProxyService
    {
        private readonly IHgServer _hgServer;
        private readonly IServerConfiguration _configuration;
        private readonly IDeploymentManager _deploymentManager;
        private readonly IRepositoryManager _repositoryManager;

        public ProxyService(IHgServer hgServer,
                               IServerConfiguration configuration,
                               IDeploymentManager deploymentManager,
                               IRepositoryManager repositoryManager)
        {
            _hgServer = hgServer;
            _configuration = configuration;
            _deploymentManager = deploymentManager;
            _repositoryManager = repositoryManager;
        }

        [WebInvoke(UriTemplate = "")]
        public HttpResponseMessage PostProxy(HttpRequestMessage request)
        {
            return ProxyRequest(request);
        }

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
                EnsureHgRepository();
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
                    proxyResponse = client.Get(uri);
                    break;
                case "POST":
                    proxyResponse = client.Post(uri, request.Content);
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

        private void EnsureHgRepository()
        {
            RepositoryUtility.EnsureRepository(_repositoryManager, RepositoryType.Mercurial);
        }
    }
}

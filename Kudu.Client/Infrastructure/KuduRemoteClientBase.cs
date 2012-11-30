using System;
using System.Net;
using System.Net.Http;

namespace Kudu.Client.Infrastructure
{
    public abstract class KuduRemoteClientBase
    {
        protected KuduRemoteClientBase(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
        {
            if (serviceUrl == null)
            {
                throw new ArgumentNullException("serviceUrl");
            }

            ServiceUrl = serviceUrl;
            Credentials = credentials;
            Client = HttpClientHelper.CreateClient(ServiceUrl, credentials, handler);
        }

        public string ServiceUrl { get; private set; }

        public ICredentials Credentials { get; private set; }

        public HttpClient Client { get; private set; }
    }
}

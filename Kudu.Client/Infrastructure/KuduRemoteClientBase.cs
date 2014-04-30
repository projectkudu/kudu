using System;
using System.Net;
using System.Net.Http;

namespace Kudu.Client.Infrastructure
{
    public abstract class KuduRemoteClientBase
    {
        protected KuduRemoteClientBase(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null, bool useCookie = false)
        {
            if (serviceUrl == null)
            {
                throw new ArgumentNullException("serviceUrl");
            }

            ServiceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            Credentials = credentials;
            Client = HttpClientHelper.CreateClient(ServiceUrl, credentials, handler, useCookie);
        }

        public string ServiceUrl { get; private set; }

        public ICredentials Credentials { get; private set; }

        public HttpClient Client { get; private set; }
    }
}

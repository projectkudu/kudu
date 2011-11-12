using System.Net;
using System.Net.Http;

namespace Kudu.Client.Infrastructure
{
    public abstract class KuduRemoteClientBase
    {
        protected readonly HttpClient _client;
        private ICredentials _credentials;

        public KuduRemoteClientBase(string serviceUrl)
        {
            ServiceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            _client = HttpClientHelper.Create(ServiceUrl);
        }

        public string ServiceUrl { get; private set; }

        public ICredentials Credentials
        {
            get
            {
                return _credentials;
            }
            set
            {
                _credentials = value;
                _client.SetClientCredentials(_credentials);
            }
        }
    }
}

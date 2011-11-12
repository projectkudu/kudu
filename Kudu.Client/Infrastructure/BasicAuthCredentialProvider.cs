using System.Net;

namespace Kudu.Client.Infrastructure
{
    public class BasicAuthCredentialProvider : ICredentialProvider
    {
        private readonly ICredentials _credentials;

        public BasicAuthCredentialProvider(string userName, string password)
        {
            _credentials = new NetworkCredential(userName, password);
        }

        public ICredentials GetCredentials()
        {
            return _credentials;
        }
    }
}

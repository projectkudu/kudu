using System.Net;

namespace Kudu.Client.Infrastructure
{
    public interface ICredentialProvider
    {
        ICredentials GetCredentials();
    }
}

using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement.Certificates.Wrappers
{
    //Note: Wrapper intention is to facilitate mocking.
    //      This class has mostly been generated with resharper.
    public interface IX509Certificate2Collection : IEnumerable<IX509Certificate2>
    {
        IX509Certificate2Collection Find(X509FindType findType, object findValue, bool validOnly);
    }
}
using System.Collections.Generic;

namespace Kudu.SiteManagement.Certificates
{
    public interface ICertificateSearcher
    {
        ICertificateLookup Lookup(string value);

        IEnumerable<Certificate> FindAll();
    }
}
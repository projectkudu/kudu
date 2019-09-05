using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement.Certificates.Wrappers
{
    //Note: Wrapper intention is to facilitate mocking.
    //      This class has mostly been generated with resharper.
    public interface IX509Store
    {
        string Name { get; }
        IX509Certificate2Collection Certificates { get; }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1726:UsePreferredTerms", MessageId = "flags", 
            Justification = "The name 'flags' originate from the X509Store class, as this is meant to be an interface for a wrapper, "
                            + "we want to keep the names intact.")]
        void Open(OpenFlags flags);
        void Close();
    }
}
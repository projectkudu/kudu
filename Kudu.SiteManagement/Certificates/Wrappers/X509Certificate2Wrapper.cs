using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement.Certificates.Wrappers
{
    //Note: Wrapper intention is to facilitate mocking.
    //      This class has mostly been generated with resharper.
    public class X509Certificate2Wrapper : IX509Certificate2
    {
        private readonly X509Certificate2 _cert;

        public string FriendlyName
        {
            get { return _cert.FriendlyName; }
        }

        public string Thumbprint
        {
            get { return _cert.Thumbprint; }
        }

        public X509Certificate2Wrapper(X509Certificate2 cert)
        {
            _cert = cert;
        }

        public byte[] GetCertHash()
        {
            return _cert.GetCertHash();
        }
    }
}
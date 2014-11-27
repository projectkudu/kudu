using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Certificates.Wrappers;

namespace Kudu.SiteManagement.Certificates
{
    public sealed class Certificate
    {
        private readonly IX509Certificate2 _certificate;

        public string StoreName { get; private set; }
        public string FriendlyName { get { return _certificate.FriendlyName; } }
        public string Thumbprint { get { return _certificate.Thumbprint; } }

        public Certificate(IX509Certificate2 certificate, string storeName)
        {
            _certificate = certificate;
            StoreName = storeName;
        }

        public byte[] GetCertHash()
        {
            return _certificate.GetCertHash();
        }
    }
}
using System;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement
{
    public interface ICertificateSearcher
    {
        X509Certificate2 LookupX509Certificate2(string certificateName, string storeName = "My");
    }

    public class CertificateSearcher : ICertificateSearcher
    {
        public X509Certificate2 LookupX509Certificate2(string certificateName, string storeName = "My")
        {
            using (var searcher = new X509StoreSearcher(storeName, StoreLocation.LocalMachine))
            {
                return searcher.Find(certificateName);
            }
        }
    }

    public sealed class X509StoreSearcher : IDisposable
    {
        private bool _disposed;
        private X509Store _store;

        public X509StoreSearcher(string storeName, StoreLocation location)
        {
            _store = new X509Store(storeName, location);
            Store.Open(OpenFlags.ReadOnly);
        }

        public X509Store Store
        {
            get { return _store; }
        }

        public X509Certificate2 Find(string name)
        {
            if (_disposed) throw new ObjectDisposedException("X509StoreSearcher");

            return Store.Certificates.OfType<X509Certificate2>()
                .FirstOrDefault(cert => cert.FriendlyName.Equals(name, StringComparison.OrdinalIgnoreCase));
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            Store.Close();
            _store = null;
            _disposed = true;
        }
    }
}
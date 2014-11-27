using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Certificates.Wrappers;

namespace Kudu.SiteManagement.Certificates
{
    public interface ICertificateLookup
    {
        Certificate ByFriendlyName();
        Certificate ByThumbprint();
    }

    public sealed class CertificateLookup : ICertificateLookup
    {
        private readonly string _value;
        private readonly IEnumerable<StoreName> _stores;
        private readonly Func<StoreName, IX509Store> _storeFactory;

        public CertificateLookup(string value, IEnumerable<StoreName> stores, Func<StoreName, IX509Store> storeFactory)
        {
            _value = value;
            _stores = stores;
            _storeFactory = storeFactory;
        }

        public Certificate ByFriendlyName()
        {
            foreach (IX509Store store in _stores.Select(storeName => _storeFactory(storeName)))
            {
                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    IX509Certificate2 certificate = store
                        .Certificates
                        .FirstOrDefault(cert => cert.FriendlyName.Equals(_value, StringComparison.OrdinalIgnoreCase));

                    if (certificate != null)
                    {
                        return new Certificate(certificate, store.Name);
                    }
                }
                finally
                {
                    store.Close();
                }
            }
            return null;
        }

        public Certificate ByThumbprint()
        {
            foreach (IX509Store store in _stores.Select(storeName => _storeFactory(storeName)))
            {
                try
                {
                    store.Open(OpenFlags.ReadOnly);
                    IX509Certificate2 certificate = store.Certificates
                        .Find(X509FindType.FindByThumbprint, _value, false)
                        .FirstOrDefault();

                    if (certificate != null)
                    {
                        return new Certificate(certificate, store.Name);
                    }
                }
                finally
                {
                    store.Close();
                }
            }
            return null;
        }
    }
}
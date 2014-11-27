using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Certificates.Wrappers;
using Kudu.SiteManagement.Configuration;

namespace Kudu.SiteManagement.Certificates
{
    public class CertificateSearcher : ICertificateSearcher
    {
        private readonly IKuduConfiguration _configuration;
        private readonly Func<StoreName, IX509Store> _storeFactory;

        public CertificateSearcher(IKuduConfiguration configuration)
            : this(configuration, (name) => new X509StoreWrapper(new X509Store(name, StoreLocation.LocalMachine)))
        {
        }

        //Note: Constructor mainly here for testing
        public CertificateSearcher(IKuduConfiguration configuration, Func<StoreName, IX509Store> storeFactory)
        {
            _configuration = configuration;
            _storeFactory = storeFactory;
        }

        public ICertificateLookup Lookup(string value)
        {
            return new CertificateLookup(value, _configuration.CertificateStores.Select(store => store.Name), _storeFactory);
        }

        public IEnumerable<Certificate> FindAll()
        {
            return _configuration
                .CertificateStores
                .SelectMany(storeCfg =>
                {
                    IX509Store store = _storeFactory(storeCfg.Name);
                    store.Open(OpenFlags.ReadOnly);
                    try
                    {
                        return store
                            .Certificates
                            .Select(cert => new Certificate(cert, store.Name))
                            .ToList();
                    }
                    finally 
                    {
                        store.Close();
                    }
                });
        }
    }
}
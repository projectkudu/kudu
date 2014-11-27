using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Configuration.Section;
using Kudu.SiteManagement.Configuration.Section.Cert;

namespace Kudu.SiteManagement.Configuration
{
    public interface ICertificateStoreConfiguration
    {
        StoreName Name { get; }
    }

    public class CertificateStoreConfiguration : ICertificateStoreConfiguration
    {
        public StoreName Name { get; private set; }

        public CertificateStoreConfiguration(CertificateStoreConfigurationElement store)
            : this(store.Name)
        {
        }

        public CertificateStoreConfiguration(StoreName name)
        {
            Name = name;
        }
    }
}
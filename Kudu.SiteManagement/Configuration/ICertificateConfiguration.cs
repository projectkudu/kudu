using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Configuration.Section;
using Kudu.SiteManagement.Configuration.Section.Cert;

namespace Kudu.SiteManagement.Configuration
{
    public interface ICertificateStoreConfiguration
    {
        string Name { get; }
    }

    public class CertificateStoreConfiguration : ICertificateStoreConfiguration
    {
        public string Name { get; private set; }

        public CertificateStoreConfiguration(CertificateStoreConfigurationElement store)
            : this(store.Name)
        {
        }

        public CertificateStoreConfiguration(string name)
        {
            Name = name;
        }
    }
}
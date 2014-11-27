using System.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement.Configuration.Section.Cert
{
    public class CertificateStoreConfigurationElement : NamedConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public StoreName Name
        {
            get { return (StoreName)this["name"]; }
        }
    }
}
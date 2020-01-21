using System.Configuration;
using System.Security.Cryptography.X509Certificates;

namespace Kudu.SiteManagement.Configuration.Section.Cert
{
    public class CertificateStoreConfigurationElement : NamedConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return (string)this["name"]; }
        }
    }
}
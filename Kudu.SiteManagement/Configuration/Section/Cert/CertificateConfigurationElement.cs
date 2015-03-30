using System.Configuration;

namespace Kudu.SiteManagement.Configuration.Section.Cert
{
    //TODO: Quick and dirty, but might keep this to give as a reference list.
    public class CertificateConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true)]
        public string Name
        {
            get { return (string)this["name"]; }
        }

        [ConfigurationProperty("store", IsRequired = false)]
        public string Store
        {
            get { return (string)this["store"]; }
        }
    }
}
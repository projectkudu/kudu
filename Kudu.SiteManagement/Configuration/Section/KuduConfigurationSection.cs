using System.Configuration;
using Kudu.SiteManagement.Configuration.Section.Bindings;
using Kudu.SiteManagement.Configuration.Section.Cert;

namespace Kudu.SiteManagement.Configuration.Section
{
    public class KuduConfigurationSection : ConfigurationSection
    {
        [ConfigurationProperty("enableCustomHostNames", IsRequired = false, DefaultValue = false)]
        public bool CustomHostNamesEnabled
        {
            get { return (bool)this["enableCustomHostNames"]; }
        }

        [ConfigurationProperty("serviceSite", IsRequired = true)]
        public PathConfigurationElement ServiceSite
        {
            get { return this["serviceSite"] as PathConfigurationElement; }
        }

        [ConfigurationProperty("applications", IsRequired = true)]
        public PathConfigurationElement Applications
        {
            get { return this["applications"] as PathConfigurationElement; }
        }

        [ConfigurationProperty("bindings", IsRequired = false)]
        public BindingsConfigurationElementCollection Bindings
        {
            get { return this["bindings"] as BindingsConfigurationElementCollection; }
        }

        [ConfigurationProperty("certificateStores", IsRequired = false)]
        public CertificateStoresConfigurationElementCollection CertificateStores
        {
            get { return this["certificateStores"] as CertificateStoresConfigurationElementCollection; }
        }
    }
}
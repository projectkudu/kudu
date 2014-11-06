using System.Configuration;

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
        
        [ConfigurationProperty("applicationBase", IsRequired = false)]
        public UrlConfigurationElement ApplicationBase
        {
            get { return this["applicationBase"] as UrlConfigurationElement; }
        }

        [ConfigurationProperty("serviceBase", IsRequired = false)]
        public UrlConfigurationElement ServiceBase
        {
            get { return this["serviceBase"] as UrlConfigurationElement; }
        }
    }
}
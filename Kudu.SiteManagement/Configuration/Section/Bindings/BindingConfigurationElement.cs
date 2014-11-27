using System.ComponentModel;
using System.Configuration;

namespace Kudu.SiteManagement.Configuration.Section.Bindings
{
    public abstract class BindingConfigurationElement : NamedConfigurationElement
    {
        public SiteType SiteType { get; private set; }

        protected BindingConfigurationElement(SiteType siteType)
        {
            SiteType = siteType;
        }

        //TODO: use [RegexStringValidator("...url pattern")] to validate
        [ConfigurationProperty("url", IsRequired = true)]
        public string Url
        {
            get { return (string)this["url"]; }
        }

        [ConfigurationProperty("scheme", IsRequired = false, DefaultValue = UriScheme.Http)]
        [TypeConverter(typeof(UriSchemeConverter))]
        public UriScheme Scheme
        {
            get { return (UriScheme)this["scheme"]; }
        }

        [ConfigurationProperty("certificate", IsRequired = false)]
        public string Certificate
        {
            get { return (string)this["certificate"]; }
        }

        [ConfigurationProperty("require-sni", IsRequired = false)]
        public bool RequireSni
        {
            get { return (bool)this["require-sni"]; }
        }

    }
}
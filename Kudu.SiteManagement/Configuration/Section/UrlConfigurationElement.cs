using System.ComponentModel;
using System.Configuration;

namespace Kudu.SiteManagement.Configuration.Section
{
    public class UrlConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("url", IsRequired = true)]
        //TODO: use [RegexStringValidator("...url pattern")] to validate
        public string Url
        {
            get { return (string)this["url"]; }
        }

        [ConfigurationProperty("scheme", IsRequired = false)]
        [TypeConverter(typeof(UriSchemeConverter))]
        public UriSchemes Scheme
        {
            get { return (UriSchemes)this["scheme"]; }
        }
    }
}
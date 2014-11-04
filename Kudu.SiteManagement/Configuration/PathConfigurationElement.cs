using System.Configuration;

namespace Kudu.SiteManagement.Configuration
{
    public class PathConfigurationElement : ConfigurationElement
    {
        [ConfigurationProperty("path", IsRequired = true)]
        public string Path
        {
            get { return (string)this["path"]; }
        }
    }
}
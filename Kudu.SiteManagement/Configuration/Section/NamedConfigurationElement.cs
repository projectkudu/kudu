using System.Configuration;
using System.Xml;

namespace Kudu.SiteManagement.Configuration.Section
{
    public abstract class NamedConfigurationElement : ConfigurationElement
    {
        public void DeserializeElement(XmlReader reader)
        {
            base.DeserializeElement(reader, false);
        }
    }
}
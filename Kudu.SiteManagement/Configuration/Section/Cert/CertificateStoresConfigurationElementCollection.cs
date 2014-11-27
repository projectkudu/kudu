using System;
using System.Configuration;

namespace Kudu.SiteManagement.Configuration.Section.Cert
{
    public class CertificateStoresConfigurationElementCollection : NamedElementCollection<CertificateStoreConfigurationElement>
    {
        protected override object GetElementKey(ConfigurationElement element)
        {
            CertificateStoreConfigurationElement store = element as CertificateStoreConfigurationElement;
            if (store == null)
                throw new ConfigurationErrorsException();

            return store.Name;
        }

        protected override Type ResolveTypeName(string elementName)
        {
            if (elementName == "store" || elementName == "certificateStore")
                return typeof(CertificateStoreConfigurationElement);

            throw new ConfigurationErrorsException();
        }
    }
}
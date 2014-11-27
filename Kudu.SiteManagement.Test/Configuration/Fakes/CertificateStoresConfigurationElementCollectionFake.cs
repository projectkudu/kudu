using Kudu.SiteManagement.Configuration.Section.Cert;

namespace Kudu.SiteManagement.Test.Configuration.Fakes
{
    public class CertificateStoresConfigurationElementCollectionFake : CertificateStoresConfigurationElementCollection
    {
        public CertificateStoresConfigurationElementCollectionFake AddFake(CertificateStoreConfigurationElement element)
        {
            base.Add(element);
            return this;
        }
       
    }
}
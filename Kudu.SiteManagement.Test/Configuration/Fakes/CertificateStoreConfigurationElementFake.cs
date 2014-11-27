using Kudu.SiteManagement.Configuration.Section.Cert;

namespace Kudu.SiteManagement.Test.Configuration.Fakes
{
    public class CertificateStoreConfigurationElementFake : CertificateStoreConfigurationElement
    {
        public CertificateStoreConfigurationElementFake SetFake(string key, object value)
        {
            this[key] = value;
            return this;
        }
    }
}
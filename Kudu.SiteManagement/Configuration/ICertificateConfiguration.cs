using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement.Configuration
{
    public interface ICertificateConfiguration
    {
        string Name { get; }
        string Store { get; }
    }

    public class CertificateConfiguration : ICertificateConfiguration
    {
        public string Name { get; private set; }
        public string Store { get; private set; }

        public CertificateConfiguration(CertificateConfigurationElement certificate)
        {
            Name = certificate.Name;
            Store = certificate.Store ?? "My";
        }
    }
}
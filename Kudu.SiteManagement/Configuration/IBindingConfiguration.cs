using Kudu.SiteManagement.Configuration.Section;
using Kudu.SiteManagement.Configuration.Section.Bindings;

namespace Kudu.SiteManagement.Configuration
{
    public interface IBindingConfiguration
    {
        string Url { get; }
        UriScheme Scheme { get; }
        SiteType SiteType { get; }
        string Certificate { get; }
        bool RequireSni { get; }
    }

    public class BindingConfiguration : IBindingConfiguration
    {
        public string Url { get; private set; }
        public UriScheme Scheme { get; private set; }
        public SiteType SiteType { get; private set; }
        public string Certificate { get; private set; }
        public bool RequireSni { get; private set; }

        public BindingConfiguration(BindingConfigurationElement binding)
            : this(binding.Url, binding.Scheme, binding.SiteType, binding.Certificate, binding.RequireSni)
        {
        }

        public BindingConfiguration(string url, UriScheme scheme, SiteType siteType)
            : this(url, scheme, siteType, null, false)
        {

        }

        public BindingConfiguration(string url, UriScheme scheme, SiteType siteType, string certificate, bool requireSni = false)
        {
            Url = url;
            Scheme = scheme;
            SiteType = siteType;
            Certificate = certificate;
            RequireSni = requireSni;
        }
    }
}
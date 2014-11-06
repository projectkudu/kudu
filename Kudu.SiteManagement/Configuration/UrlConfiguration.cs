using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement.Configuration
{
    public interface IUrlConfiguration
    {
        string Url { get; }
        UriSchemes Scheme { get; }
    }

    public class UrlConfiguration : IUrlConfiguration
    {
        public string Url { get; private set; }
        public UriSchemes Scheme { get; private set; }

        public UrlConfiguration(UrlConfigurationElement serviceBase)
            : this(serviceBase.Url, serviceBase.Scheme)
        {
        }

        public UrlConfiguration(string url, UriSchemes scheme)
        {
            Url = url;
            Scheme = scheme;
        }
    }
}
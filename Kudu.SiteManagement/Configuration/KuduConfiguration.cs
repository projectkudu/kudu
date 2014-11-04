using System.Configuration;

namespace Kudu.SiteManagement.Configuration
{
    public interface IKuduConfiguration
    {
        string RootPath { get; }

        UrlConfigurationElement ServiceBase { get; }
        UrlConfigurationElement ApplicationBase { get; }
        PathConfigurationElement Applications { get; }
        PathConfigurationElement ServiceSite { get; }

        bool CustomHostNamesEnabled { get; }
    }

    public class KuduConfiguration : IKuduConfiguration
    {
        public static IKuduConfiguration Load(string root)
        {
            return new KuduConfiguration(root, ConfigurationManager.GetSection("kudu.management") as KuduConfigurationSection);
        }

        //Note: Simple delegation for now. Ideally we would like this incapsulation for a number of reasons:
        //      - Deal with legacy settings defined directly in appsettings. (If we wan't to maintain backwards compatibility)
        //      - We can provide meaningfull default values for more complex configurations with more ease.
        //      - Mocking purposes in tests.
        //
        //      Eventually this may replace the IPathResolver and ISettingsResolver
        private readonly KuduConfigurationSection _section;

        public string RootPath { get; private set; }

        public UrlConfigurationElement ServiceBase
        {
            get { return _section.ServiceBase; }
        }

        public UrlConfigurationElement ApplicationBase
        {
            get { return _section.ApplicationBase; }
        }

        public PathConfigurationElement Applications
        {
            get { return _section.Applications; }
        }

        public PathConfigurationElement ServiceSite
        {
            get { return _section.ServiceSite; }
        }

        public bool CustomHostNamesEnabled
        {
            get { return _section.CustomHostNamesEnabled; }
        }

        private KuduConfiguration(string root, KuduConfigurationSection section)
        {
            RootPath = root;
            _section = section;
        }
    }
}

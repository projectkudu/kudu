using System.ComponentModel;
using System.Configuration;
using System.IO;
using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement.Configuration
{
    public interface IKuduConfiguration
    {
        string RootPath { get; }
        string SitesPath { get; }
        string ServiceSitePath { get; }
        
        bool CustomHostNamesEnabled { get; }

        IUrlConfiguration ServiceBase { get; }
        IUrlConfiguration ApplicationBase { get; }
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

        public bool CustomHostNamesEnabled
        {
            get
            {
                if (_section != null)
                    return _section.CustomHostNamesEnabled;

                bool value;
                bool.TryParse(ConfigurationManager.AppSettings["enableCustomHostNames"], out value);
                //Note: try parse will default the bool if it fails. that means null.
                return value;
            }
        }

        public string ServiceSitePath
        {
            get
            {
                return _section == null
                    ? PathRelativeToRoot(ConfigurationManager.AppSettings["serviceSitePath"])
                    : PathRelativeToRoot(_section.ServiceSite.Path);
            }
        }

        public string SitesPath
        {
            get
            {
                return _section == null
                    ? PathRelativeToRoot(ConfigurationManager.AppSettings["sitesPath"])
                    : PathRelativeToRoot(_section.Applications.Path);
            }
        }

        public IUrlConfiguration ServiceBase
        {
            get
            {
                if(_section != null)
                    return new UrlConfiguration(_section.ServiceBase);

                string legacy = ConfigurationManager.AppSettings["serviceUrlBaseValue"];
                return legacy != null 
                    ? new UrlConfiguration(legacy.TrimStart('.'), UriScheme.Http) 
                    : null;

                //TODO: Can we return a meaningfull "null" implementation here instead?
            }
        }

        public IUrlConfiguration ApplicationBase
        {
            get
            {
                if (_section != null)
                    return new UrlConfiguration(_section.ApplicationBase);

                string legacy = ConfigurationManager.AppSettings["urlBaseValue"];
                return legacy != null 
                    ? new UrlConfiguration(legacy.TrimStart('.'), UriScheme.Http) 
                    : null;

                //TODO: Can we return a meaningfull "null" implementation here instead?
            }
        }

        private KuduConfiguration(string root, KuduConfigurationSection section)
        {
            RootPath = root;
            _section = section;
        }

        public string PathRelativeToRoot(string path)
        {
            return Path.Combine(RootPath, path);
        }
    }
}

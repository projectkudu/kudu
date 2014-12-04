using System.ComponentModel;
using System.Configuration;
using System.IO;
using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement.Configuration
{
    public interface IKuduConfiguration
    {
        string RootPath { get; }
        string ApplicationsPath { get; }
        string ServiceSitePath { get; }
        bool CustomHostNamesEnabled { get; }

        IUrlConfiguration ServiceBase { get; }
        IUrlConfiguration ApplicationBase { get; }

        ICertificateConfiguration Certificate { get; }
    }

    public class KuduConfiguration : IKuduConfiguration
    {
        public static IKuduConfiguration Load(string root)
        {
            return new KuduConfiguration(root, ConfigurationManager.GetSection("kudu.management") as KuduConfigurationSection);
        }

        private readonly KuduConfigurationSection _section;

        public string RootPath { get; private set; }

        //TODO: Temporary dirty aproach to test it out in the IIS.
        public ICertificateConfiguration Certificate
        {
            get
            {
                return _section != null ? new CertificateConfiguration(_section.Certificate) : null;
            }
        }

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
                //TODO: The original Path Resolver had this:
                //      that seems a bit odd?
                //      const string @default = @"%SystemDrive%\KuduService\wwwroot";
                //                   @default = Environment.ExpandEnvironmentVariables(@default);
                //      if(Directory.Exists(@default)) return @default;
                //
                //  - Do we wan't to do that, basically ignoring configuration?...

                return _section == null
                    ? PathRelativeToRoot(ConfigurationManager.AppSettings["serviceSitePath"])
                    : PathRelativeToRoot(_section.ServiceSite.Path);

            }
        }

        public string ApplicationsPath
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
                    ? new UrlConfiguration(legacy.TrimStart('.'), UriSchemes.Http) 
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
                    ? new UrlConfiguration(legacy.TrimStart('.'), UriSchemes.Http) 
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
            return Path.GetFullPath(Path.Combine(RootPath, path));
        }
    }
}

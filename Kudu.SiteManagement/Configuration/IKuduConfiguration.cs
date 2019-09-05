using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Web;
using Kudu.SiteManagement.Configuration.Section;
using Kudu.Client.Infrastructure;

namespace Kudu.SiteManagement.Configuration
{
    public interface IKuduConfiguration
    {
        string RootPath { get; }
        string ApplicationsPath { get; }
        string ServiceSitePath { get; }
        string IISConfigurationFile { get; }
        bool CustomHostNamesEnabled { get; }
        BasicAuthCredentialProvider BasicAuthCredential { get; }

        IEnumerable<IBindingConfiguration> Bindings { get; }
        IEnumerable<ICertificateStoreConfiguration> CertificateStores { get; }
    }

    public class KuduConfiguration : IKuduConfiguration
    {
        public static IKuduConfiguration Load(string root)
        {
            return new KuduConfiguration(root, ConfigurationManager.GetSection("kudu.management") as KuduConfigurationSection, ConfigurationManager.AppSettings);
        }

        private readonly KuduConfigurationSection _section;
        private readonly NameValueCollection _appSettings;

        public string RootPath { get; private set; }

        public bool CustomHostNamesEnabled
        {
            get
            {
                if (_section != null)
                {
                    return _section.CustomHostNamesEnabled;
                }

                bool value;
                bool.TryParse(_appSettings["enableCustomHostNames"], out value);
                //NOTE: try parse will default the bool to false if it fails.
                //      this will catch cases as null and invalid with the intended behavior.
                return value;
            }
        }

        public string ServiceSitePath
        {
            get
            {
                //RFC:  The original Path Resolver had this:
                //      that seems a bit odd?
                //      const string @default = @"%SystemDrive%\KuduService\wwwroot";
                //                   @default = Environment.ExpandEnvironmentVariables(@default);
                //      if(Directory.Exists(@default)) return @default;
                //
                //  - Do we want to do that, basically ignoring configuration?...

                return _section == null
                    ? PathRelativeToRoot(_appSettings["serviceSitePath"])
                    : PathRelativeToRoot(_section.ServiceSite.Path);
            }
        }

        public string ApplicationsPath
        {
            get
            {
                return _section == null
                    ? PathRelativeToRoot(_appSettings["sitesPath"])
                    : PathRelativeToRoot(_section.Applications.Path);
            }
        }

        private const string DefaultIisConfigurationFile = "%windir%\\system32\\inetsrv\\config\\applicationHost.config";

        public string IISConfigurationFile
        {
            get
            {
                if (_section == null || _section.IisConfigurationFile == null)
                {
                    return Environment.ExpandEnvironmentVariables(DefaultIisConfigurationFile);
                }

                return string.IsNullOrEmpty(_section.IisConfigurationFile.Path)
                    ? Environment.ExpandEnvironmentVariables(DefaultIisConfigurationFile)
                    : _section.IisConfigurationFile.Path;
            }
        }

        public IEnumerable<IBindingConfiguration> Bindings
        {
            get
            {
                if (_section == null || _section.Bindings == null)
                {
                    return Enumerable.Empty<IBindingConfiguration>()
                        .Union(LegacyBinding("urlBaseValue", SiteType.Live))
                        .Union(LegacyBinding("serviceUrlBaseValue", SiteType.Service));
                }

                return _section.Bindings.Items.Select(binding => new BindingConfiguration(binding));
            }
        }

        public IEnumerable<ICertificateStoreConfiguration> CertificateStores
        {
            get
            {
                if (_section == null || _section.CertificateStores == null || !_section.CertificateStores.Items.Any())
                {
                    return new[] { new CertificateStoreConfiguration(StoreName.My) };
                }

                return _section.CertificateStores.Items.Select(store => new CertificateStoreConfiguration(store));
            }
        }

        public BasicAuthCredentialProvider BasicAuthCredential
        {
            get
            {
                if ( _section == null ||
                    _section.BasicAuthCredential == null ||
                    string.IsNullOrEmpty( _section.BasicAuthCredential.Username ) ||
                    string.IsNullOrEmpty( _section.BasicAuthCredential.Password ) ) {
                    return new BasicAuthCredentialProvider( "admin" , "kudu" );
                }
                return new BasicAuthCredentialProvider( _section.BasicAuthCredential.Username , _section.BasicAuthCredential.Password );
            }
        }


        private KuduConfiguration(string root, KuduConfigurationSection section, NameValueCollection appSettings)
        {
            RootPath = root;
            _section = section;
            _appSettings = appSettings;
        }

        private string PathRelativeToRoot(string path)
        {
            string combined = Path.Combine(RootPath, path);
            return Path.GetFullPath(combined);
        }

        private IEnumerable<IBindingConfiguration> LegacyBinding(string key, SiteType type)
        {
            string legacyBinding = _appSettings[key];
            if (string.IsNullOrEmpty(legacyBinding))
            {
                yield break;
            }
            yield return new BindingConfiguration(legacyBinding, UriScheme.Http, type, null);
        }


    }
}

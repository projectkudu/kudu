using System;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Configuration;
using Kudu.SiteManagement.Configuration.Section;
using Kudu.SiteManagement.Configuration.Section.Cert;
using Kudu.SiteManagement.Test.Configuration.Fakes;
using Xunit;

namespace Kudu.SiteManagement.Test.Configuration
{
    public class KuduConfigurationFacts
    {
        [Fact]
        public void CustomHostNamesEnabled_NoConfiguration_DefaultsToFalse()
        {
            var appSettingsFake = new NameValueCollection();

            IKuduConfiguration config = CreateConfiguration(null, appSettingsFake);
            Assert.Equal(false, config.CustomHostNamesEnabled);
        }

        [Fact]
        public void CustomHostNamesEnabled_WithoutConfigurationSection_ReturnsLegacySetting()
        {
            var appSettingsFake = new NameValueCollection();
            appSettingsFake.Add("enableCustomHostNames", "true");

            IKuduConfiguration config = CreateConfiguration(null, appSettingsFake);
            Assert.Equal(true, config.CustomHostNamesEnabled);
        }

        [Fact]
        public void CustomHostNamesEnabled_WithoutConfigurationSectionInvalid_ReturnsFalse()
        {
            var appSettingsFake = new NameValueCollection();
            appSettingsFake.Add("enableCustomHostNames", "fubar");

            IKuduConfiguration config = CreateConfiguration(null, appSettingsFake);
            Assert.Equal(false, config.CustomHostNamesEnabled);
        }

        [Fact]
        public void CustomHostNamesEnabled_WithConfigurationSectionTrue_ReturnsTrue()
        {
            var configFake = new KuduConfigurationSectionFake();
            configFake.SetFake("enableCustomHostNames", true);

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            Assert.Equal(true, config.CustomHostNamesEnabled);
        }

        [Fact]
        public void ApplicationsPath_WithoutConfigurationSection_ReturnsCombinedPathUsingLegacySeeting()
        {
            var appSettingsFake = new NameValueCollection();
            appSettingsFake.Add("sitesPath", ".\\sitespath");

            IKuduConfiguration config = CreateConfiguration(null, appSettingsFake);
            string expected = Path.GetFullPath(".\\root_path\\sitespath");
            Assert.Equal(expected, config.ApplicationsPath);
        }

        [Fact]
        public void ApplicationsPath_WithConfigurationSection_ReturnsCombinedPath()
        {
            var configFake = new KuduConfigurationSectionFake();
            configFake.SetFake("applications", PathConfigurationElementFake.Fake("path", ".\\sitespath"));

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            string expected = Path.GetFullPath(".\\root_path\\sitespath");
            Assert.Equal(expected, config.ApplicationsPath);
        }

        [Fact]
        public void ServiceSitePath_WithoutConfigurationSection_ReturnsCombinedPathUsingLegacySeeting()
        {
            var appSettingsFake = new NameValueCollection();
            appSettingsFake.Add("serviceSitePath", ".\\servicepath");

            IKuduConfiguration config = CreateConfiguration(null, appSettingsFake);
            string expected = Path.GetFullPath(".\\root_path\\servicepath");
            Assert.Equal(expected, config.ServiceSitePath);
        }

        [Fact]
        public void ServiceSitePath_WithConfigurationSection_ReturnsSetting()
        {
            var configFake = new KuduConfigurationSectionFake();
            configFake.SetFake("serviceSite", PathConfigurationElementFake.Fake("path", ".\\servicepath"));

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            string expected = Path.GetFullPath(".\\root_path\\servicepath");
            Assert.Equal(expected, config.ServiceSitePath);
        }

        [Fact]
        public void Bindings_LegacyApplicationBinding_MapsToBindingConfiguration()
        {
            var appSettingsFake = new NameValueCollection();
            appSettingsFake.Add("urlBaseValue", "kudu.domain.com");

            IKuduConfiguration config = CreateConfiguration(null, appSettingsFake);
            Assert.Equal(1, config.Bindings.Count());
            Assert.Equal(UriScheme.Http, config.Bindings.First().Scheme);
            Assert.Equal("kudu.domain.com", config.Bindings.First().Url);
            Assert.Equal(SiteType.Live, config.Bindings.First().SiteType);
        }

        [Fact]
        public void Bindings_LegacyServiceBinding_MapsToBindingConfiguration()
        {
            var appSettingsFake = new NameValueCollection();
            appSettingsFake.Add("serviceUrlBaseValue", "kudu.svc.domain.com");

            IKuduConfiguration config = CreateConfiguration(null, appSettingsFake);
            Assert.Equal(1, config.Bindings.Count());
            Assert.Equal(UriScheme.Http, config.Bindings.First().Scheme);
            Assert.Equal("kudu.svc.domain.com", config.Bindings.First().Url);
            Assert.Equal(SiteType.Service, config.Bindings.First().SiteType);
        }

        [Fact]
        public void Bindings_SingleApplicationBinding_MapsToBindingConfiguration()
        {
            var configFake = new KuduConfigurationSectionFake();
            var bindingsFake = new BindingsConfigurationElementCollectionFake();
            bindingsFake.AddFake(new ApplicationBindingConfigurationElementFake()
                .SetFake("scheme", UriScheme.Http)
                .SetFake("url", "kudu.domain.com"));

            configFake.SetFake("bindings", bindingsFake);

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            Assert.Equal(1, config.Bindings.Count());
            Assert.Equal(UriScheme.Http, config.Bindings.First().Scheme);
            Assert.Equal("kudu.domain.com", config.Bindings.First().Url);
            Assert.Equal(SiteType.Live, config.Bindings.First().SiteType);
        }

        [Fact]
        public void Bindings_SingleServiceBinding_MapsToBindingConfiguration()
        {
            var configFake = new KuduConfigurationSectionFake();
            var bindingsFake = new BindingsConfigurationElementCollectionFake();
            bindingsFake.AddFake(new ServiceBindingConfigurationElementFake()
                .SetFake("scheme", UriScheme.Http)
                .SetFake("url", "kudu.svc.domain.com"));

            configFake.SetFake("bindings", bindingsFake);

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            Assert.Equal(1, config.Bindings.Count());
            Assert.Equal(UriScheme.Http, config.Bindings.First().Scheme);
            Assert.Equal("kudu.svc.domain.com", config.Bindings.First().Url);
            Assert.Equal(SiteType.Service, config.Bindings.First().SiteType);
        }


        [Fact]
        public void CertificateStores_WithoutStoresConfiguration_DefaultsToSingleStoreMy()
        {
            IKuduConfiguration config = CreateConfiguration(null, new NameValueCollection());
            Assert.Equal(StoreName.My, config.CertificateStores.Single().Name);
        }

        [Fact]
        public void CertificateStores_WithEmptyConfigurationSection_DefaultsToSingleStoreMy()
        {
            var configFake = new KuduConfigurationSectionFake();
            var storesFake = new CertificateStoresConfigurationElementCollectionFake();
            configFake.SetFake("certificateStores", storesFake);

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            Assert.Equal(StoreName.My, config.CertificateStores.Single().Name);
        }

        [Fact]
        public void CertificateStores_WithSingleElement_ConstainsSingleItem()
        {
            var configFake = new KuduConfigurationSectionFake();
            var storesFake = new CertificateStoresConfigurationElementCollectionFake();
            storesFake.Add(new CertificateStoreConfigurationElementFake()
                .SetFake("name", StoreName.Root));

            configFake.SetFake("certificateStores", storesFake);

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            Assert.Equal(StoreName.Root, config.CertificateStores.Single().Name);
        }

        [Fact]
        public void CertificateStores_WithMultipleElements_ConstainsSingleItem()
        {
            var configFake = new KuduConfigurationSectionFake();
            var storesFake = new CertificateStoresConfigurationElementCollectionFake();
            storesFake.Add(new CertificateStoreConfigurationElementFake()
                .SetFake("name", StoreName.Root));
            storesFake.Add(new CertificateStoreConfigurationElementFake()
                .SetFake("name", StoreName.My));

            configFake.SetFake("certificateStores", storesFake);

            IKuduConfiguration config = CreateConfiguration(configFake, new NameValueCollection());
            Assert.Equal(2, config.CertificateStores.Count());
            Assert.Equal(StoreName.Root, config.CertificateStores.ElementAt(0).Name);
            Assert.Equal(StoreName.My, config.CertificateStores.ElementAt(1).Name);
        }

        private IKuduConfiguration CreateConfiguration(KuduConfigurationSectionFake configFake, NameValueCollection appSettingsFake)
        {
            Type type = typeof(KuduConfiguration);
            ConstructorInfo ctor = type.GetConstructor(BindingFlags.NonPublic | BindingFlags.Instance, null,
                new[] { typeof(string), typeof(KuduConfigurationSection), typeof(NameValueCollection) }, null);
            return (IKuduConfiguration)ctor.Invoke(new object[] { "root_path", configFake, appSettingsFake });
        }
    }
}
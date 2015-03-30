using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Certificates;
using Kudu.SiteManagement.Certificates.Wrappers;
using Kudu.SiteManagement.Configuration;
using Kudu.SiteManagement.Test.Certificates.Fakes;
using Moq;
using Xunit;

namespace Kudu.SiteManagement.Test.Certificates
{
    public class CertificateSearcherFacts
    {
        [Fact]
        public void FindAll_MultipleStores_ReturnsFromAllStores()
        {
            Mock<IKuduConfiguration> configMock = new Mock<IKuduConfiguration>();
            configMock.Setup(mock => mock.CertificateStores)
                .Returns(new[] { new CertificateStoreConfiguration(StoreName.My), new CertificateStoreConfiguration(StoreName.Root) });
            
            Dictionary<StoreName, Mock<IX509Store>> storeMocks = new Dictionary<StoreName, Mock<IX509Store>>();
            storeMocks[StoreName.My] = CreateX509StoreMock(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(friendlyName: "My_CertA"),
                new X509Certificate2Fake(friendlyName: "My_CertB")
            });
            storeMocks[StoreName.Root] = CreateX509StoreMock(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(friendlyName: "Root_CertA"),
                new X509Certificate2Fake(friendlyName: "Root_CertB")
            });

            ICertificateSearcher searcher = new CertificateSearcher(configMock.Object, name => storeMocks[name].Object);
            Dictionary<string, Certificate> all = searcher.FindAll().ToDictionary(c => c.FriendlyName);

            Assert.Equal(all.Count, 4);
            Assert.NotNull(all["My_CertA"]);
            Assert.NotNull(all["My_CertB"]);
            Assert.NotNull(all["Root_CertA"]);
            Assert.NotNull(all["Root_CertB"]);
        }

        [Fact]
        public void Lookup_ReturnsCertificateLookupObject()
        {
            Mock<IKuduConfiguration> configMock = new Mock<IKuduConfiguration>();
            configMock.Setup(mock => mock.CertificateStores).Returns(new[] { new CertificateStoreConfiguration(StoreName.My) });

            ICertificateSearcher searcher = new CertificateSearcher(configMock.Object, null);
            ICertificateLookup result = searcher.Lookup("FindMe");

            Assert.IsType<CertificateLookup>(result);
        }
        
        private static Mock<IX509Store> CreateX509StoreMock(IX509Certificate2Collection fake)
        {
            Mock<IX509Store> mock = new Mock<IX509Store>();
            mock.Setup(m => m.Certificates).Returns(fake);
            return mock;
        }
    }
}
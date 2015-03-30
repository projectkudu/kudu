using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using Kudu.SiteManagement.Certificates;
using Kudu.SiteManagement.Certificates.Wrappers;
using Kudu.SiteManagement.Test.Certificates.Fakes;
using Moq;
using Xunit;

namespace Kudu.SiteManagement.Test.Certificates
{
    public class CertificateLookupFacts
    {
        [Fact]
        public void ByThumbprint_OneMatching_ReturnsCertificate()
        {
            Mock<IX509Store> storeMock = new Mock<IX509Store>();
            IX509Certificate2Collection collectionFake = new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake(),
                new X509Certificate2Fake(friendlyName: "FindMe", thumbprint: "FindMe")
            };
            storeMock.Setup(mock => mock.Certificates).Returns(collectionFake);
            
            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My }, name => storeMock.Object)
                .ByThumbprint();

            Assert.Equal(result.FriendlyName, "FindMe");
            Assert.Equal(result.Thumbprint, "FindMe");
        }

        [Fact]
        public void ByFriendlyName_OneMatching_ReturnsCertificate()
        {
            Mock<IX509Store> storeMock = new Mock<IX509Store>();
            storeMock.Setup(mock => mock.Certificates).Returns(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake(),
                new X509Certificate2Fake(friendlyName: "FindMe", thumbprint: "FindMe")
            });

            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My }, name => storeMock.Object)
                .ByFriendlyName();

            Assert.Equal(result.FriendlyName, "FindMe");
            Assert.Equal(result.Thumbprint, "FindMe");
        }

        [Fact]
        public void ByThumbprint_NoneMatching_ReturnsNull()
        {
            Mock<IX509Store> storeMock = new Mock<IX509Store>();
            IX509Certificate2Collection collectionFake = new X509Certificate2CollectionFake { new X509Certificate2Fake() };
            storeMock.Setup(mock => mock.Certificates).Returns(collectionFake);

            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My }, name => storeMock.Object)
                .ByThumbprint();

            Assert.Null(result);
        }

        [Fact]
        public void ByFriendlyName_NoneMatching_ReturnsNull()
        {
            Mock<IX509Store> storeMock = new Mock<IX509Store>();
            storeMock.Setup(mock => mock.Certificates).Returns(new X509Certificate2CollectionFake { new X509Certificate2Fake() });

            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My }, name => storeMock.Object)
                .ByFriendlyName();

            Assert.Null(result);
        }

        [Fact]
        public void ByThumbprint_MultipleMatching_ReturnsFirstMatchingCertificate()
        {
            Mock<IX509Store> storeMock = new Mock<IX509Store>();
            storeMock.Setup(mock => mock.Certificates).Returns(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake(friendlyName: "FindMe", thumbprint: "FindMe"),
                new X509Certificate2Fake(friendlyName: "NotMe", thumbprint: "FindMe")
            });

            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My }, name => storeMock.Object)
                .ByThumbprint();

            Assert.Equal(result.FriendlyName, "FindMe");
            Assert.Equal(result.Thumbprint, "FindMe");
        }

        [Fact]
        public void ByFriendlyName_MultipleMatching_ReturnsFirstMatchingCertificate()
        {
            Mock<IX509Store> storeMock = new Mock<IX509Store>();
            storeMock.Setup(mock => mock.Certificates).Returns(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake(friendlyName: "FindMe", thumbprint: "FindMe"),
                new X509Certificate2Fake(friendlyName: "FindMe", thumbprint: "NotMe")
            });

            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My }, name => storeMock.Object)
                .ByFriendlyName();

            Assert.Equal(result.FriendlyName, "FindMe");
            Assert.Equal(result.Thumbprint, "FindMe");
        }

        [Fact]
        public void ByThumbprint_OneMatchingInSecondaryStore_ReturnsCertificate()
        {
            Dictionary<StoreName, Mock<IX509Store>> storeMocks = new Dictionary<StoreName, Mock<IX509Store>>();
            storeMocks[StoreName.My] = CreateX509StoreMock(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake()
            });
            storeMocks[StoreName.Root] = CreateX509StoreMock(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake(friendlyName: "FindMe", thumbprint: "FindMe")
            });

            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My, StoreName.Root }, name => storeMocks[name].Object)
                .ByThumbprint();

            Assert.Equal(result.FriendlyName, "FindMe");
            Assert.Equal(result.Thumbprint, "FindMe");
        }

        [Fact]
        public void ByFriendlyName_OneMatchingInSecondaryStore_ReturnsCertificate()
        {
            Dictionary<StoreName, Mock<IX509Store>> storeMocks = new Dictionary<StoreName, Mock<IX509Store>>();
            storeMocks[StoreName.My] = CreateX509StoreMock(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake()
            });
            storeMocks[StoreName.Root] = CreateX509StoreMock(new X509Certificate2CollectionFake
            {
                new X509Certificate2Fake(),
                new X509Certificate2Fake(friendlyName: "FindMe", thumbprint: "FindMe")
            });

            Certificate result = new CertificateLookup("FindMe", new[] { StoreName.My, StoreName.Root }, name => storeMocks[name].Object)
                .ByFriendlyName();

            Assert.Equal(result.FriendlyName, "FindMe");
            Assert.Equal(result.Thumbprint, "FindMe");
        }

        private static Mock<IX509Store> CreateX509StoreMock(IX509Certificate2Collection fake)
        {
            Mock<IX509Store> mock = new Mock<IX509Store>();
            mock.Setup(m => m.Certificates).Returns(fake);
            return mock;
        }
    }
}
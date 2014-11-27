using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.SiteManagement.Configuration;
using Moq;
using Xunit;

namespace Kudu.SiteManagement.Test
{
    public class PathResolverFacts
    {
        [Fact]
        public void GetApplicationPath_WithApplicationsPath_CombinesNameAndPath()
        {
            Mock<IKuduConfiguration> mock = new Mock<IKuduConfiguration>();
            mock.Setup(x => x.ApplicationsPath).Returns("C:\\Dummy\\Path");

            IPathResolver resolver = new PathResolver(mock.Object);

            Assert.Equal(resolver.GetApplicationPath("Foo"), "C:\\Dummy\\Path\\Foo");
        }

        [Fact]
        public void GetLiveSitePath_WithApplicationsPath_CombinesNameAndPathWithSiteAdded()
        {
            Mock<IKuduConfiguration> mock = new Mock<IKuduConfiguration>();
            mock.Setup(x => x.ApplicationsPath).Returns("C:\\Dummy\\Path");

            IPathResolver resolver = new PathResolver(mock.Object);

            Assert.Equal(resolver.GetLiveSitePath("Foo"), "C:\\Dummy\\Path\\Foo\\site");
        }
    }
}

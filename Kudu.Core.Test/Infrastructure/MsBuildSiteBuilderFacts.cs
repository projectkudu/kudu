using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kudu.Core.Deployment;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    public class MsBuildSiteBuilderFacts
    {
        [Fact]
        public void GetPropertyStringReturnsProperStringWith1Property()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            properties["sanity"] = "sane";

            this.RunGetPropertyStringScenario(properties, @"sanity=""sane""");
        }

        [Fact]
        public void GetPropertyStringReturnsProperStringWith3Properties()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();
            properties["sanity"] = "sane";
            properties["sanity1"] = "sane1";
            properties["sanity2"] = "sane2";

            this.RunGetPropertyStringScenario(properties, @"sanity=""sane"";sanity1=""sane1"";sanity2=""sane2""");
        }

        [Fact]
        public void GetPropertyStringReturnsProperStringWithNoPreperties()
        {
            Dictionary<string, string> properties = new Dictionary<string, string>();

            this.RunGetPropertyStringScenario(properties, string.Empty);
        }

        private void RunGetPropertyStringScenario(Dictionary<string, string> properties, string expectedResult)
        {
            var buildPropertyProviderTest = new BuildPropertyProviderTest(properties);
            var siteBuilder = new MsBuildSiteBuilderTest(buildPropertyProviderTest);
            var result = siteBuilder.GetPropertyStringForTest();

            Assert.Equal(expectedResult, result);
        }

        private class MsBuildSiteBuilderTest : MsBuildSiteBuilder
        {
            public MsBuildSiteBuilderTest(BuildPropertyProviderTest buildPropertyProviderTest)
                : base(buildPropertyProviderTest, Path.GetTempPath(), Path.GetTempPath(), Path.GetTempPath())
            {
            }

            public override Task Build(DeploymentContext context)
            {
                throw new NotImplementedException();
            }

            public string GetPropertyStringForTest()
            {
                return this.GetPropertyString();
            }
        }

        private class BuildPropertyProviderTest : IBuildPropertyProvider
        {
            private Dictionary<string, string> properties;

            public BuildPropertyProviderTest(Dictionary<string, string> properties)
            {
                this.properties = properties;
            }

            public IDictionary<string, string> GetProperties()
            {
                return this.properties;
            }
        }

    }
}
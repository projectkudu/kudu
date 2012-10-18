using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Kudu.Core.Deployment;
using Xunit;
using Xunit.Extensions;

namespace Kudu.Core.Infrastructure.Test
{
    public class MsBuildSiteBuilderFacts
    {
        [Theory]
        [PropertyData("GetPropertyStringData")]
        private void RunGetPropertyStringScenario(Dictionary<string, string> properties, string expectedResult)
        {
            var buildPropertyProviderTest = new BuildPropertyProviderTest(properties);
            var siteBuilder = new MsBuildSiteBuilderTest(buildPropertyProviderTest);
            var result = siteBuilder.GetPropertyStringForTest();

            Assert.Equal(expectedResult, result);
        }

        public static IEnumerable<object[]> GetPropertyStringData
        {
            get
            {
                Dictionary<string, string> properties = new Dictionary<string, string>();
                properties["sanity"] = "sane";
                yield return new object[] { properties, @"sanity=""sane""" };

                properties = new Dictionary<string, string>();
                properties["sanity"] = "sane";
                properties["sanity1"] = "sane1";
                properties["sanity2"] = "sane2";
                yield return new object[] { properties, @"sanity=""sane"";sanity1=""sane1"";sanity2=""sane2""" };

                properties = new Dictionary<string, string>();
                yield return new object[] { properties, String.Empty };
            }
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
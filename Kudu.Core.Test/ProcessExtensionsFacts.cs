using System.Collections.Generic;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;
using Xunit;

namespace Kudu.Core.Test
{
    public class ProcessExtensionsFacts
    {
        [Theory]
        [InlineData(null, false)]
        [InlineData("", false)]
        [InlineData("NotSCMAppPool", false)]
        [InlineData("~1SCMAppPool", true)]
        [InlineData("~1SCMAppPool", true)]
        public void GetIsScmSite_ReturnsExpectedResult(string appPoolId, bool expectedValue)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            if (appPoolId != null)
            {
                environment[WellKnownEnvironmentVariables.ApplicationPoolId] = appPoolId;
            }

            Assert.Equal(expectedValue, ProcessExtensions.GetIsScmSite(environment));
        }

        [Theory]
        [InlineData(null, false)]
        [InlineData("", true)]
        [InlineData("TestWebJob", true)]
        public void GetIsWebJob_ReturnsExpectedResult(string webJobName, bool expectedValue)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            if (webJobName != null)
            {
                environment[WellKnownEnvironmentVariables.WebJobsName] = webJobName;
            }

            Assert.Equal(expectedValue, ProcessExtensions.GetIsWebJob(environment));
        }

        [Theory]
        [InlineData(null, null, null)]
        [InlineData("TestJob", null, null)]
        [InlineData(null, "Continuous", null)]
        [InlineData("TestJob", "Continuous", "WebJob: TestJob, Type: Continuous")]
        [InlineData("", "", "WebJob: , Type: ")]
        public void GetDescription_WebJob_ReturnsExpectedResult(string webJobName, string webJobType, string expectedValue)
        {
            Dictionary<string, string> environment = new Dictionary<string, string>();
            if (webJobName != null)
            {
                environment[WellKnownEnvironmentVariables.WebJobsName] = webJobName;
            }
            if (webJobType != null)
            {
                environment[WellKnownEnvironmentVariables.WebJobsType] = webJobType;
            }

            Assert.Equal(expectedValue, ProcessExtensions.GetDescription(environment));
        }
    }
}

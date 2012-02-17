using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Kudu.Core.Deployment;
using Xunit;

namespace Kudu.FunctionalTests.Infrastructure
{
    public static class KuduAssert
    {
        public static void VerifyUrl(string url, string content = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var client = new HttpClient();
            var response = client.GetAsync(url).Result;
            Assert.Equal(statusCode, response.StatusCode);
            if (content != null)
            {
                var responseBody = response.Content.ReadAsStringAsync().Result;
                Assert.True(responseBody.Contains(content));
            }
        }

        internal static void VerifyMsBuildOutput(string expectedMatch, ApplicationManager appManager, List<DeployResult> results)
        {
            var entries = appManager.DeploymentManager.GetLogEntriesAsync(results[0].Id).Result.ToList();
            Assert.Equal(3, entries.Count);
            var details = appManager.DeploymentManager.GetLogEntryDetailsAsync(results[0].Id, entries[1].EntryId).Result.ToList();
            Assert.Equal(4, details.Count);
            Assert.True(details[2].Message.Contains(expectedMatch));
        }
    }
}

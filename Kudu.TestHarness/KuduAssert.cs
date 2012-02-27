using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit;

namespace Kudu.TestHarness
{
    public static class KuduAssert
    {
        public static T ThrowsUnwrapped<T>(Action action) where T : Exception
        {
            var ex = Assert.Throws<AggregateException>(() => action());
            var baseEx = ex.GetBaseException();
            Assert.IsType<T>(baseEx);
            return (T)baseEx;
        }

        public static void VerifyUrl(string url, params string[] contents)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
            var response = client.GetAsync(url).Result;
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            if (contents.Length > 0)
            {
                var responseBody = response.Content.ReadAsStringAsync().Result;
                Assert.True(contents.All(responseBody.Contains));
            }
        }

        public static void VerifyUrl(string url, string content = null, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var client = new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
            var response = client.GetAsync(url).Result;
            Assert.Equal(statusCode, response.StatusCode);
            if (content != null)
            {
                var responseBody = response.Content.ReadAsStringAsync().Result;
                Assert.True(responseBody.Contains(content));
            }
        }

        public static void VerifyLogOutput(ApplicationManager appManager, string id, params string[] expectedMatches)
        {
            var entries = appManager.DeploymentManager.GetLogEntriesAsync(id).Result.ToList();
            Assert.Equal(3, entries.Count);
            var allDetails = entries.SelectMany(e => appManager.DeploymentManager.GetLogEntryDetailsAsync(id, e.Id).Result).ToList();
            var allEntries = entries.Concat(allDetails).ToList();
            Assert.True(expectedMatches.All(match => allDetails.Any(e => e.Message.Contains(match))));
        }
    }
}

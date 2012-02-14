using System.Net;
using System.Net.Http;
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
    }
}

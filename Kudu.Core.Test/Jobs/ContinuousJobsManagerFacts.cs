using Kudu.Core.Jobs;
using Kudu.Core.Tracing;
using Kudu.TestHarness;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Kudu.Core.Test.Jobs
{
    public class ContinuousJobsManagerFacts
    {
        [Theory]
        [InlineData(4567, "path/to/webjob/function", @"{""key"": ""value""}")]
        public void CloneRequestTestCase(int port, string path, string content)
        {
            // Arrange
            var headers = new Dictionary<string, string> { { "key", "value" }, { "HOST", "example.com" } };
            var request = new HttpRequestMessage(HttpMethod.Put, new Uri("https://kudu.com/" + path));
            request.Content = new StringContent(content);
            foreach (var pair in headers)
                request.Headers.Add(pair.Key, pair.Value);

            // Act
            var clone = ContinuousJobsManager.GetForwardRequest(port, path, request);

            // Assert
            Assert.Equal(request.Method, clone.Method);
            Assert.Equal(string.Format("http://127.0.0.1:{0}/{1}", port, path), clone.RequestUri.AbsoluteUri);
            Assert.Equal(content, clone.Content.ReadAsStringAsync().Result);
            Assert.Equal(1, clone.Headers.Count());
            Assert.Equal("value", clone.Headers.First().Value.First());
        }
    }
}

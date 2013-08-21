using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Kudu.Services.Test
{
    public delegate HttpResponseMessage MessageDelegate(HttpRequestMessage message);

    /// <remarks>
    /// Adapted from http://stackoverflow.com/questions/9789944/how-can-i-test-a-custom-delegatinghandler-in-the-asp-net-mvc-4-web-api/9789952#9789952
    /// </remarks>
    public class TestMessageHandler : DelegatingHandler
    {
        private readonly MessageDelegate handlerFunc;

        public TestMessageHandler(HttpStatusCode statusCode)
        {
            this.handlerFunc = _ => new HttpResponseMessage(statusCode);
        }

        public TestMessageHandler(string content, bool isJson = false)
        {
            var stringContent = new StringContent(content);
            if (isJson)
            {
                stringContent.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            }
            this.handlerFunc = _ => new HttpResponseMessage(HttpStatusCode.OK) { Content = stringContent };
        }

        public TestMessageHandler(HttpResponseMessage response)
        {
            this.handlerFunc = _ => response;
        }

        public TestMessageHandler(MessageDelegate handlerFunc)
        {
            this.handlerFunc = handlerFunc;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            HttpResponseMessage response = this.handlerFunc(request);
            return Task.FromResult(response);
        }

        public bool Disposed { get; set; }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Disposed = true;
        }
    }
}

using System.Web;

namespace Kudu.Services.Infrastructure
{
    public static class HttpRequestExtensions
    {
        public static string GetRequestId(this HttpRequest httpRequest)
        {
            // prefer x-arr-log-id over x-ms-request-id since azure always populates the former.
            return httpRequest.Headers[Constants.ArrLogIdHeader] ?? httpRequest.Headers[Constants.RequestIdHeader];
        }

        public static string GetUserAgent(this HttpRequest httpRequest)
        {
            return httpRequest.UserAgent ?? string.Empty;
        }
    }
}
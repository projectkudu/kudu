using System.Net.Http;

namespace Kudu.Core.Infrastructure {
    internal static class HttpClientHelper {
        public static HttpClient Create(string url) {
            // The URL needs to end with a slash for HttpClient to do the right thing with relative paths
            url = UrlUtility.EnsureTrailingSlash(url);

            return new HttpClient(url) {
                MaxResponseContentBufferSize = 30 * 1024 * 1024
            };
        }
    }
}

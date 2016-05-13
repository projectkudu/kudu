using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;

namespace Kudu.Services.Infrastructure
{
    public static class UriHelper
    {
        public static Uri GetBaseUri(HttpRequestMessage request)
        {
            IEnumerable<string> disguisedHostValues = new List<string>();

            // Azure will always pass this header to Kudu, and it always carry the right host name.
            // when running Kudu on mono in a container, container is bound to a local ip
            // so we cannot rely on "request.RequestUri", which will be "127.0.0.1:xxxx".
            if (request.Headers.TryGetValues("DISGUISED-HOST", out disguisedHostValues) && disguisedHostValues.Count() > 0)
            {
                return new UriBuilder("https", disguisedHostValues.First()).Uri;
            }

            return new Uri(request.RequestUri.GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
        }

        public static Uri MakeRelative(Uri baseUri, string relativeUri)
        {
            var builder = new UriBuilder(baseUri);
            // We don't care about the query string
            builder.Query = null;
            if (builder.Port == 80)
            {
                builder.Port = -1;
            }
            baseUri = new Uri(EnsureTrailingSlash(builder.ToString()));
            return new Uri(baseUri, relativeUri);
        }

        internal static string EnsureTrailingSlash(string url)
        {
            if (url.EndsWith("/", StringComparison.Ordinal))
            {
                return url;
            }

            return url + "/";
        }
    }
}

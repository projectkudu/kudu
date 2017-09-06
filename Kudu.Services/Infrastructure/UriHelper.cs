using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using Kudu.Core.Helpers;

namespace Kudu.Services.Infrastructure
{
    public static class UriHelper
    {
        public static Uri GetBaseUri(HttpRequestMessage request)
        {
            return new Uri(GetRequestUri(request).GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
        }

        public static Uri GetRequestUri(HttpRequestMessage request)
        {
            var uri = request.RequestUri;

            // On Linux, corrections to the request URI are needed due to the way the request is handled on the worker:
            // - Set scheme to https
            // - Set host to the value of DISGUISED-HOST
            // - Remove port value
            IEnumerable<string> disguisedHostValues;
            if (!OSDetector.IsOnWindows()
                && request.Headers.TryGetValues("DISGUISED-HOST", out disguisedHostValues)
                && disguisedHostValues.Count() > 0)
            {
                uri = (new UriBuilder(uri)
                {
                    Scheme = "https",
                    Host = disguisedHostValues.First(),
                    Port = -1
                }).Uri;
            }

            return uri;
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

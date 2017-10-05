using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Web;
using Kudu.Core.Helpers;

namespace Kudu.Services.Infrastructure
{
    public static class UriHelper
    {
        private const string DisguisedHostHeaderName = "DISGUISED-HOST";

        public static Uri GetBaseUri(HttpRequestMessage request)
        {
            return new Uri(GetRequestUri(request).GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
        }

        public static Uri GetRequestUri(HttpRequestMessage request)
        {
            string disguisedHost = null;

            IEnumerable<string> disguisedHostValues;
            if (request.Headers.TryGetValues(DisguisedHostHeaderName, out disguisedHostValues)
                && disguisedHostValues.Count() > 0)
            {
                disguisedHost = disguisedHostValues.First();
            }

            return GetRequestUriInternal(request.RequestUri, disguisedHost);
        }

        public static Uri GetRequestUri(HttpRequest request)
        {
            return GetRequestUriInternal(request.Url, request.Headers[DisguisedHostHeaderName]);
        }

        private static Uri GetRequestUriInternal(Uri uri, string disguisedHostValue)
        {
            // On Linux, corrections to the request URI are needed due to the way the request is handled on the worker:
            // - Set scheme to https
            // - Set host to the value of DISGUISED-HOST
            // - Remove port value
            if (!OSDetector.IsOnWindows() && disguisedHostValue != null)
            {
                uri = (new UriBuilder(uri)
                {
                    Scheme = "https",
                    Host = disguisedHostValue,
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

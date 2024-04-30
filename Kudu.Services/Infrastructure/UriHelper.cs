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
        private const string ForwardedHostHeaderName = "X-FORWARDED-HOST";
        private const string OriginalHostHeaderName = "X-ORIGINAL-HOST";

        public static Uri GetBaseUri(this HttpRequestMessage request, bool useOriginalHost = false)
        {
            return new Uri(request.GetRequestUri(useOriginalHost).GetComponents(UriComponents.SchemeAndServer, UriFormat.Unescaped));
        }

        public static Uri GetRequestUri(this HttpRequestMessage request, bool useOriginalHost = false)
        {
            string disguisedHost = null;
            IEnumerable<string> values;

            if (useOriginalHost)
            {
                if (request.Headers.TryGetValues(ForwardedHostHeaderName, out values)
                    && values.Count() > 0)
                {
                    disguisedHost = values.First();
                }

                if (string.IsNullOrWhiteSpace(disguisedHost)
                    && request.Headers.TryGetValues(OriginalHostHeaderName, out values)
                    && values.Count() > 0)
                {
                    disguisedHost = values.First();
                }
            }

            // On Linux, corrections to the request URI are needed due to the way the request is handled on the worker:
            // - Set scheme to https
            // - Set host to the value of DISGUISED-HOST
            // - Remove port value
            if (!OSDetector.IsOnWindows()
                && string.IsNullOrWhiteSpace(disguisedHost)
                && request.Headers.TryGetValues(DisguisedHostHeaderName, out values)
                && values.Count() > 0)
            {
                disguisedHost = values.First();
            }

            return GetRequestUriInternal(request.RequestUri, disguisedHost);
        }

        public static Uri GetRequestUri(this HttpRequest request, bool useOriginalHost = false)
        {
            string disguisedHost = null;

            if (useOriginalHost)
            {
                disguisedHost = request.Headers[ForwardedHostHeaderName];
                if (string.IsNullOrWhiteSpace(disguisedHost))
                {
                    disguisedHost = request.Headers[OriginalHostHeaderName];
                }
            }

            // On Linux, corrections to the request URI are needed due to the way the request is handled on the worker:
            // - Set scheme to https
            // - Set host to the value of DISGUISED-HOST
            // - Remove port value
            if (!OSDetector.IsOnWindows()
                && string.IsNullOrWhiteSpace(disguisedHost))
            {
                disguisedHost = request.Headers[DisguisedHostHeaderName];
            }

            return GetRequestUriInternal(request.Url, disguisedHost);
        }

        private static Uri GetRequestUriInternal(Uri uri, string disguisedHostValue)
        {
            if (!string.IsNullOrWhiteSpace(disguisedHostValue))
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

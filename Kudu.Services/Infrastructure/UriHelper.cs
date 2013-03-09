using System;

namespace Kudu.Services.Infrastructure
{
    public static class UriHelper
    {
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

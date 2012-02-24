using System;

namespace Kudu.Services.Infrastructure
{
    public static class UriHelper
    {
        public static Uri MakeRelative(Uri baseUri, string relativeUri)
        {
            baseUri = new Uri(EnsureTrailingSlash(baseUri.OriginalString));
            return new Uri(baseUri, relativeUri);
        }

        private static string EnsureTrailingSlash(string url)
        {
            if (url.EndsWith("/"))
            {
                return url;
            }

            return url + "/";
        }
    }
}

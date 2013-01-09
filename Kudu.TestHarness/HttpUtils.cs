using System;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;

namespace Kudu.TestHarness
{
    public class HttpUtils
    {
        public static HttpResponseMessage WaitForSite(string siteUrl, ICredentials credentials = null, int retries = 5, int delayBeforeFirstTry = 0, int delayBeforeRetry = 250, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            Thread.Sleep(delayBeforeFirstTry);

            HttpResponseMessage response = null;

            var client = new HttpClient(new WebRequestHandler()
            {
                Credentials = credentials,
                // Disable caching to make sure we always get fresh data from the test site
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore)
            });

            client.Timeout = TimeSpan.FromSeconds(200);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
            client.MaxResponseContentBufferSize = 30 * 1024 * 1024;
            while (retries > 0)
            {
                try
                {
                    response = client.GetAsync(siteUrl).Result;
                    if (response.StatusCode == statusCode)
                    {
                        break;
                    }
                }
                catch
                {
                    if (retries == 0)
                    {
                        throw;
                    }
                }

                retries--;

                if (retries > 0)
                {
                    Thread.Sleep(delayBeforeRetry);
                }
                else
                {
                    throw new Exception(string.Format("Web site {0} is not available", siteUrl));
                }
            }

            return response;
        }
    }
}

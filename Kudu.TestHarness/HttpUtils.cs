using System;
using System.Net;
using System.Net.Cache;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.TestHarness
{
    public class HttpUtils
    {
        public static HttpResponseMessage WaitForSite(string siteUrl, ICredentials credentials = null, int retries = 5, int delayBeforeFirstTry = 0, int delayBeforeRetry = 250, HttpStatusCode statusCode = HttpStatusCode.OK, string httpMethod = "GET", string jsonPayload = "")
        {
            return WaitForSiteAsync(siteUrl, credentials, retries, delayBeforeFirstTry, delayBeforeRetry, statusCode, httpMethod, jsonPayload).Result;
        }

        public static async Task<HttpResponseMessage> WaitForSiteAsync(string siteUrl, ICredentials credentials = null, int retries = 5, int delayBeforeFirstTry = 0, int delayBeforeRetry = 250, HttpStatusCode statusCode = HttpStatusCode.OK, string httpMethod = "GET", string jsonPayload = "")
        {
            if (delayBeforeFirstTry > 0)
            {
                await Task.Delay(delayBeforeFirstTry);
            }

            HttpResponseMessage response = null;
            HttpClient client = new HttpClient(new WebRequestHandler
            {
                Credentials = credentials,
                // Disable caching to make sure we always get fresh data from the test site
                CachePolicy = new RequestCachePolicy(RequestCacheLevel.NoCacheNoStore)
            });
            client.Timeout = TimeSpan.FromSeconds(200);
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Kudu-Test", "1.0"));
            client.MaxResponseContentBufferSize = 30 * 1024 * 1024;

            Exception lastException = null;
            while (true)
            {
                try
                {
                    if (String.Equals(httpMethod, "POST"))
                    {
                        response = await client.PostAsync(siteUrl, new StringContent(jsonPayload, Encoding.UTF8, "application/json"));
                    }
                    else
                    {
                        response = await client.GetAsync(siteUrl);
                    }

                    if (response.StatusCode == statusCode)
                    {
                        return response;
                    }

                    throw new Exception(string.Format("Mismatch response status {0} != {1}", statusCode, response.StatusCode));
                }
                catch (Exception ex)
                {
                    lastException = ex;
                }

                retries--;

                var message = string.Format("Wait for site {0} failed with {1}", siteUrl, lastException.Message);

                if (retries > 0)
                {
                    await Task.Delay(delayBeforeRetry);
                }
                else
                {
                    throw new Exception(message, lastException);
                }
            }
        }
    }
}

using System.Net;
using System.Net.Http;
using System.Threading;
using Kudu.Client.Infrastructure;

namespace Kudu.TestHarness
{
    public class HttpUtils
    {
        public static void WaitForSite(string siteUrl, ICredentials credentials = null, int retries = 3, int delayBeforeRetry = 250)
        {
            var handler = HttpClientHelper.CreateClientHandler(siteUrl, credentials);
            var client = new HttpClient(handler);

            while (retries > 0)
            {
                try
                {
                    var response = client.GetAsync(siteUrl).Result;
                    if (response.StatusCode == HttpStatusCode.OK)
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
                Thread.Sleep(delayBeforeRetry);
            }
        }
    }
}

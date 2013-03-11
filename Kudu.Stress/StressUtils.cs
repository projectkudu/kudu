using System;
using System.Collections.Generic;
using System.Configuration;
using System.Net;
using System.Net.Http;
using System.Linq;
using System.Text;
using System.Threading; 
using System.Threading.Tasks;
using System.Xml;


namespace Kudu.Stress
{
    class StressUtils
    {
        public static void VerifySite(string url, string validationTxt)
        {
            HttpResponseMessage httpResponse = null ;
            string responseContent = null ;
            
            RetryWithTryCatch("Validate site address", 3, TimeSpan.FromSeconds(10), TimeSpan.FromMinutes(2), () =>
            {
                HttpClientHandler handler = new HttpClientHandler() ;
                var httpClient = new HttpClient(handler);
                httpResponse = httpClient.GetAsync(new Uri(url)).Result;
                if (httpResponse.StatusCode != HttpStatusCode.OK)
                {
                    string log = string.Format("URL validation error: Failed to validate Site Address. Http Status Code: {0}   for site: {1}", httpResponse.StatusCode, url);
                    throw new ApplicationException(log);
                }
            });

            Task<string> contentTask = httpResponse.Content.ReadAsStringAsync();
            responseContent = contentTask.Result;
            if (!responseContent.Contains(validationTxt))
            {
                string msg = string.Format("URL validation error:  Failed to validate content for site {0}.   Received content was:  {1}", url, responseContent);
                throw new ApplicationException(msg);
            }
        }


        public static void RetryWithTryCatch(string operationDescription, int numRetries, TimeSpan delayBetweenRetries, TimeSpan maxCompletionTime, Action operation)
        {
            int retryCount = 0;
            DateTime startTime = DateTime.Now;

            for (retryCount = 0; retryCount < numRetries; retryCount++)
            {
                try
                {
                    operation() ;
                }
                catch (Exception ex)
                {
                    if (retryCount + 1 >= numRetries)
                    {
                        throw new ApplicationException(string.Format("Exception during operation:  {0},  Exception Info:  {1}", operationDescription, ex.ToString()));
                    }

                    Thread.CurrentThread.Join(delayBetweenRetries);

                    if (DateTime.Now - startTime > maxCompletionTime)
                    {
                        throw new ApplicationException("The folowing operation failed to complete within the alloted time:  " + operationDescription);
                    }
                    continue;
                }
                break;

            }
        }

    }
}

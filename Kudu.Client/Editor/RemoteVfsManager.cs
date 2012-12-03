using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Kudu.Client.Infrastructure;

namespace Kudu.Client.Editor
{
    public class RemoteVfsManager : KuduRemoteClientBase
    {
        public RemoteVfsManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(UrlUtility.EnsureTrailingSlash(serviceUrl), credentials, handler)
        {
        }

        public string ReadAllText(string path)
        {
            HttpResponseMessage response = Client.GetAsync(path).Result;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return response
                .EnsureSuccessful()
                .Content
                .ReadAsStringAsync()
                .Result;
        }

        public void WriteAllText(string path, string content)
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Put;
                request.RequestUri = new Uri(path, UriKind.Relative);
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                request.Content = new StringContent(content);
                Client.SendAsync(request).Result.EnsureSuccessful();
            }
        }

        public void Delete(string path)
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Delete;
                request.RequestUri = new Uri(path, UriKind.Relative);
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);

                HttpResponseMessage response = Client.SendAsync(request).Result;

                // Don't throw if we got a 404, since we want to act as a 'delete if exists'
                if (response.StatusCode != HttpStatusCode.NotFound)
                {
                    response.EnsureSuccessful();
                }
            }
        }
    }
}

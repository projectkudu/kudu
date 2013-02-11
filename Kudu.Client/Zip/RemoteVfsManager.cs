using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using Kudu.Client.Infrastructure;

namespace Kudu.Client.Editor
{
    public class RemoteZipManager : KuduRemoteClientBase
    {
        public RemoteZipManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public Stream GetZipStream(string path)
        {
            HttpResponseMessage response = Client.GetAsync(path).Result;
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            return response
                .EnsureSuccessful()
                .Content
                .ReadAsStreamAsync()
                .Result;
        }

        public void PutZipStream(string path, Stream zipFile)
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Put;
                request.RequestUri = new Uri(path, UriKind.Relative);
                request.Headers.IfMatch.Add(EntityTagHeaderValue.Any);
                request.Content = new StreamContent(zipFile);
                Client.SendAsync(request).Result.EnsureSuccessful();
            }
        }


        public void PutZipFile(string path, string localZipPath)
        {
            using (var stream = File.OpenRead(localZipPath))
            {
                PutZipStream(path, stream);
            }
        }
    }
}


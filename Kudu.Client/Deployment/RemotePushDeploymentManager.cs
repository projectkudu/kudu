using System.IO;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using System;

namespace Kudu.Client.Deployment
{
    public class RemotePushDeploymentManager : KuduRemoteClientBase
    {
        public RemotePushDeploymentManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public void PushDeployFromStream(Stream zipFile, bool doAsync = false)
        {
            using (var request = new HttpRequestMessage())
            {
                if (doAsync)
                {
                    request.RequestUri = new Uri(Client.BaseAddress + "?isAsync=true");
                }

                request.Method = HttpMethod.Post;
                request.Content = new StreamContent(zipFile);
                Client.SendAsync(request).Result.EnsureSuccessful();
            }
        }

        public void PushDeployFromFile(string path, bool doAsync = false)
        {
            using (var stream = File.OpenRead(path))
            {
                PushDeployFromStream(stream, doAsync);
            }
        }
    }
}

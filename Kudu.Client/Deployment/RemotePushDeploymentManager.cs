using System.IO;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;

namespace Kudu.Client.Deployment
{
    public class RemotePushDeploymentManager : KuduRemoteClientBase
    {
        public RemotePushDeploymentManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public void PushDeployFromStream(Stream zipFile)
        {
            using (var request = new HttpRequestMessage())
            {
                request.Method = HttpMethod.Post;
                request.Content = new StreamContent(zipFile);
                Client.SendAsync(request).Result.EnsureSuccessful();
            }
        }

        public void PushDeployFromFile(string path)
        {
            using (var stream = File.OpenRead(path))
            {
                PushDeployFromStream(stream);
            }
        }
    }
}

using System.IO;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Kudu.Client.Deployment
{
    public class RemotePushDeploymentManager : KuduRemoteClientBase
    {
        public RemotePushDeploymentManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public async Task<HttpResponseMessage> PushDeployFromStream(Stream zipFile, ZipDeployMetadata metadata, IList<KeyValuePair<string, string>> queryParams = null)
        {
            using (var request = new HttpRequestMessage())
            {
                var parms = new List<string>();

                if (metadata.IsAsync)
                {
                    parms.Add("isAsync=true");
                }

                var map = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("author", metadata.Author),
                    new KeyValuePair<string, string>("authorEmail", metadata.AuthorEmail),
                    new KeyValuePair<string, string>("deployer", metadata.Deployer),
                    new KeyValuePair<string, string>("message", metadata.Message),
                };

                if (queryParams != null)
                {
                    map.AddRange(queryParams);
                }

                foreach (var item in map)
                {
                    if (item.Value != null)
                    {
                        parms.Add(
                            String.Format("{0}={1}", item.Key, item.Value));
                    }
                }

                if (parms.Any())
                {
                    request.RequestUri = new Uri(Client.BaseAddress + "?" + String.Join("&", parms));
                }

                request.Method = HttpMethod.Post;
                request.Content = new StreamContent(zipFile);
                return await Client.SendAsync(request);
            }
        }

        public async Task<HttpResponseMessage> PushDeployFromFile(string path, ZipDeployMetadata metadata)
        {
            using (var stream = File.OpenRead(path))
            {
                return await PushDeployFromStream(stream, metadata);
            }
        }
    }

    public class ZipDeployMetadata
    {
        public bool IsAsync { get; set; }
        public string Author { get; set; }
        public string AuthorEmail { get; set; }
        public string Deployer { get; set; }
        public string Message { get; set; }
    }
}

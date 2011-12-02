using System;
using System.Collections.Generic;
using Kudu.Client.Infrastructure;
using Kudu.Core.Editor;

namespace Kudu.Client.Editor
{
    public class RemoteProjectSystem : KuduRemoteClientBase, IProjectSystem
    {
        public RemoteProjectSystem(string serviceUrl)
            :base(serviceUrl)
        {
        }

        public string ReadAllText(string path)
        {
            // REVIEW: Do we need to url encode?
            // REVIEW: this goes through the same client that set the Accept header to application/json, but we receive text/plain
            return _client.GetAsync("?path=" + path)
                          .Result
                          .EnsureSuccessful()
                          .Content
                          .ReadAsString();
        }

        public Project GetProject()
        {
            return _client.GetAsyncJson<Project>(String.Empty);
        }

        public void WriteAllText(string path, string content)
        {
            _client.PostAsync("save", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path), new KeyValuePair<string, string>("content", content)))
                   .Result
                   .EnsureSuccessful();
        }

        public void Delete(string path)
        {
            _client.PostAsync("delete", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                   .Result
                   .EnsureSuccessful();
        }
    }
}

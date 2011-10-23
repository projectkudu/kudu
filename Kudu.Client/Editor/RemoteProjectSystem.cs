using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.Editor;

namespace Kudu.Client.Editor {
    public class RemoteProjectSystem : IProjectSystem {
        private readonly HttpClient _client;

        public RemoteProjectSystem(string serviceUrl) {
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public string ReadAllText(string path) {
            // REVIEW: Do we need to url encode?
            // REVIEW: this goes through the same client that set the Accept header to application/json, but we receive text/plain
            return _client.Get("?path=" + path)
                          .EnsureSuccessful()
                          .Content
                          .ReadAsString();
        }

        public Project GetProject() {
            return _client.GetJson<Project>(String.Empty);
        }

        public void WriteAllText(string path, string content) {
            _client.Post("save", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, object>("path", path), new KeyValuePair<string, object>("content", content)))
                   .EnsureSuccessful();
        }

        public void Delete(string path) {
            _client.Post("delete", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, object>("path", path)))
                   .EnsureSuccessful();
        }
    }
}

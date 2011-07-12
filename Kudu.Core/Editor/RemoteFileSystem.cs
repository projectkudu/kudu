using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Editor {
    public class RemoteFileSystem : IFileSystem {
        private readonly HttpClient _client;

        public RemoteFileSystem(string serviceUrl) {
            // The URL needs to end with a slash for HttpClient to do the right thing with relative paths
            if (!serviceUrl.EndsWith("/")) {
                serviceUrl += "/";
            }

            _client = new HttpClient(serviceUrl);
            _client.MaxResponseContentBufferSize = 30 * 1024 * 1024;
        }

        public string ReadAllText(string path) {
            // REVIEW: Do we need to url encode?
            return _client.Get("?path=" + path)
                          .EnsureSuccessStatusCode()
                          .Content
                          .ReadAsString();
        }

        public IEnumerable<string> GetFiles() {
            return _client.GetJson<string[]>(String.Empty);
        }

        public void WriteAllText(string path, string content) {
            _client.Post("save", new FormUrlEncodedContent(new Dictionary<string, string> {
                    { "path", path },
                    { "content", content }
            })).EnsureSuccessStatusCode();
        }

        public void Delete(string path) {
            _client.Post("delete", new FormUrlEncodedContent(new Dictionary<string, string> {
                    { "path", path }
            })).EnsureSuccessStatusCode();
        }
    }
}

using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Editor {
    public class RemoteFileSystem : IEditorFileSystem {
        private readonly HttpClient _client;

        public RemoteFileSystem(string serviceUrl) {
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public string ReadAllText(string path) {
            // REVIEW: Do we need to url encode?
            return _client.Get("?path=" + path)
                          .EnsureSuccessful()
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
            })).EnsureSuccessful();
        }

        public void Delete(string path) {
            _client.Post("delete", new FormUrlEncodedContent(new Dictionary<string, string> {
                    { "path", path }
            })).EnsureSuccessful();
        }
    }
}

using System.Collections.Generic;
using System.Net.Http;
using Newtonsoft.Json;

namespace Kudu.Core.SourceControl {
    public class RemoteRepository : IRepository {
        private readonly HttpClient _client;

        public RemoteRepository(string serviceUrl) {
            // The URL needs to end with a slash for HttpClient to do the right thing with relative paths
            if (!serviceUrl.EndsWith("/")) {
                serviceUrl += "/";
            }

            _client = new HttpClient(serviceUrl);
        }

        public string CurrentId {
            get {
                return _client.Get("id").EnsureSuccessStatusCode()
                              .Content
                              .ReadAsString();
            }
        }

        public void Initialize() {
            _client.Post("init", new StringContent(null))
                   .EnsureSuccessStatusCode();
        }

        public IEnumerable<Branch> GetBranches() {
            return GetJson<IEnumerable<Branch>>("branches");
        }

        public IEnumerable<FileStatus> GetStatus() {
            return GetJson<IEnumerable<FileStatus>>("status");
        }

        public IEnumerable<ChangeSet> GetChanges() {
            return GetJson<IEnumerable<ChangeSet>>("log");
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit) {
            return GetJson<IEnumerable<ChangeSet>>("log?index=" + index + "&limit=" + limit);
        }

        public ChangeSetDetail GetDetails(string id) {
            return GetJson<ChangeSetDetail>("details/" + id);
        }

        public ChangeSetDetail GetWorkingChanges() {
            return GetJson<ChangeSetDetail>("working");
        }

        public void AddFile(string path) {
            _client.Post("add", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "path", path }
            })).EnsureSuccessStatusCode();
        }

        public void RemoveFile(string path) {
            _client.Post("remove", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "path", path }
            })).EnsureSuccessStatusCode();
        }

        public ChangeSet Commit(string authorName, string message) {
            string json = _client.Post("commit", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "name", authorName },
                { "message", message }
            })).EnsureSuccessStatusCode().Content.ReadAsString();

            return JsonConvert.DeserializeObject<ChangeSet>(json);
        }

        public void Update(string id) {
            _client.Post("update", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "id", id }
            })).EnsureSuccessStatusCode();
        }

        private T GetJson<T>(string url) {
            var json = _client.Get(url).EnsureSuccessStatusCode().Content.ReadAsString();
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}

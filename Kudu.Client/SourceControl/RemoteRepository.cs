using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.SourceControl;
using Newtonsoft.Json;

namespace Kudu.Client.SourceControl
{
    public class RemoteRepository : IRepository, IKuduClientCredentials
    {
        private readonly HttpClient _client;
        private ICredentials _credentials;

        public RemoteRepository(string serviceUrl)
        {
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public ICredentials Credentials
        {
            get
            {
                return this._credentials;
            }
            set
            {
                this._credentials = value;
                this._client.SetClientCredentials(this._credentials);
            }
        }

        public string CurrentId
        {
            get
            {
                return _client.Get("id").EnsureSuccessful()
                              .Content
                              .ReadAsString();
            }
        }

        public void Initialize()
        {
            _client.Post("init", new StringContent(String.Empty))
                   .EnsureSuccessStatusCode();
        }

        public IEnumerable<Branch> GetBranches()
        {
            return _client.GetJson<IEnumerable<Branch>>("branches");
        }

        public IEnumerable<FileStatus> GetStatus()
        {
            return _client.GetJson<IEnumerable<FileStatus>>("status");
        }

        public IEnumerable<ChangeSet> GetChanges()
        {
            return _client.GetJson<IEnumerable<ChangeSet>>("log");
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit)
        {
            return _client.GetJson<IEnumerable<ChangeSet>>("log?index=" + index + "&limit=" + limit);
        }

        public ChangeSetDetail GetDetails(string id)
        {
            return _client.GetJson<ChangeSetDetail>("details/" + id);
        }

        public ChangeSetDetail GetWorkingChanges()
        {
            return _client.GetJson<ChangeSetDetail>("working");
        }

        public void AddFile(string path)
        {
            _client.Post("add", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                   .EnsureSuccessful();
        }

        public void RevertFile(string path)
        {
            _client.Post("remove", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                   .EnsureSuccessful();
        }

        public ChangeSet Commit(string authorName, string message)
        {
            string json = _client.Post("commit", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("name", authorName), new KeyValuePair<string, string>("message", message)))
                                 .EnsureSuccessful()
                                 .Content.ReadAsString();

            return JsonConvert.DeserializeObject<ChangeSet>(json);
        }

        public void Push()
        {
            _client.Post("push", new StringContent(String.Empty))
                   .EnsureSuccessStatusCode();
        }

        public void Update(string id)
        {
            _client.Post("update", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                   .EnsureSuccessful();
        }
    }
}

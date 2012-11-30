using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.SourceControl;
using Newtonsoft.Json;

namespace Kudu.Client.SourceControl
{
    public class RemoteRepository : KuduRemoteClientBase
    {
        public RemoteRepository(string serviceUrl, ICredentials credentials = null)
            : base(UrlUtility.EnsureTrailingSlash(serviceUrl), credentials)
        {
        }

        public string CurrentId
        {
            get
            {
                return Client.GetAsync("id")
                              .Result
                              .EnsureSuccessful()
                              .Content
                              .ReadAsStringAsync()
                              .Result;
            }
        }

        public void Initialize()
        {
            Client.PostAsync("init", new StringContent(String.Empty))
                  .Result
                  .EnsureSuccessful();
        }

        public IEnumerable<Branch> GetBranches()
        {
            return Client.GetJson<IEnumerable<Branch>>("branches");
        }

        public IEnumerable<FileStatus> GetStatus()
        {
            return Client.GetJson<IEnumerable<FileStatus>>("status");
        }

        public IEnumerable<ChangeSet> GetChanges()
        {
            return Client.GetJson<IEnumerable<ChangeSet>>("log");
        }

        public IEnumerable<ChangeSet> GetChanges(int index, int limit)
        {
            return Client.GetJson<IEnumerable<ChangeSet>>("log?index=" + index + "&limit=" + limit);
        }

        public ChangeSetDetail GetDetails(string id)
        {
            return Client.GetJson<ChangeSetDetail>("details/" + id);
        }

        public ChangeSet GetChangeSet(string id)
        {
            // Not used by client apis as yet
            throw new NotImplementedException();
        }

        public ChangeSetDetail GetWorkingChanges()
        {
            return Client.GetJson<ChangeSetDetail>("working");
        }

        public void AddFile(string path)
        {
            Client.PostAsync("add", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                  .Result
                  .EnsureSuccessful();
        }

        public void RevertFile(string path)
        {
            Client.PostAsync("remove", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                  .Result
                  .EnsureSuccessful();
        }

        public ChangeSet Commit(string message, string authorName)
        {
            string json = Client.PostAsync("commit", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("name", authorName), new KeyValuePair<string, string>("message", message)))
                                .Result
                                .EnsureSuccessful()
                                .Content.ReadAsStringAsync()
                                .Result;

            return JsonConvert.DeserializeObject<ChangeSet>(json);
        }

        public void Push()
        {
            Client.PostAsync("push", new StringContent(String.Empty))
                  .Result
                  .EnsureSuccessful();
        }

        public void Update(string id)
        {
            Client.PostAsync("update", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
                  .Result
                  .EnsureSuccessful();
        }

        public void Update()
        {
            Client.PostAsync("update", new StringContent(String.Empty))
                  .Result
                  .EnsureSuccessful();
        }
    }
}

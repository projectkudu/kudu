using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.SourceControl;
using Newtonsoft.Json;
using System.Diagnostics.CodeAnalysis;

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
            using (var stringContent = new StringContent(String.Empty))
            {
                Client.PostAsync("init", stringContent)
                      .Result
                      .EnsureSuccessful();
            }
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

        [SuppressMessage("Microsoft.Performance", "CA1822:MarkMembersAsStatic", Justification = "By design")]
        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", MessageId = "id", Justification = "By design")]
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
            using (var jsonContent = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
            {
                Client.PostAsync("add", jsonContent)
                      .Result
                      .EnsureSuccessful();
            }
        }

        public void RevertFile(string path)
        {
            using (var jsonContent = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
            {
                Client.PostAsync("remove", jsonContent)
                      .Result
                      .EnsureSuccessful();
            }
        }

        public ChangeSet Commit(string message, string authorName)
        {
            using (var jsonContent = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("name", authorName), new KeyValuePair<string, string>("message", message)))
            {
                string json = Client.PostAsync("commit", jsonContent)
                                    .Result
                                    .EnsureSuccessful()
                                    .Content.ReadAsStringAsync()
                                    .Result;

                return JsonConvert.DeserializeObject<ChangeSet>(json);
            }
        }

        public void Push()
        {
            using (var stringContent = new StringContent(String.Empty))
            {
                Client.PostAsync("push", stringContent)
                      .Result
                      .EnsureSuccessful();
            }
        }

        public void Update(string id)
        {
            using (var jsonContent = HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("id", id)))
            {
                Client.PostAsync("update", jsonContent)
                      .Result
                      .EnsureSuccessful();
            }
        }

        public void Update()
        {
            using (var stringContent = new StringContent(String.Empty))
            {
                Client.PostAsync("update", stringContent)
                      .Result
                      .EnsureSuccessful();
            }
        }
    }
}

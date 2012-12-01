using System;
using System.Collections.Generic;
using System.Net;
using Kudu.Client.Infrastructure;
using Kudu.Core.Editor;

namespace Kudu.Client.Editor
{
    public class RemoteProjectSystem : KuduRemoteClientBase, IProjectSystem
    {
        public RemoteProjectSystem(string serviceUrl, ICredentials credentials = null)
            : base(UrlUtility.EnsureTrailingSlash(serviceUrl), credentials)
        {
        }

        public string ReadAllText(string path)
        {
            // REVIEW: Do we need to url encode?
            // REVIEW: this goes through the same client that set the Accept header to application/json, but we receive text/plain
            return Client.GetAsync(path)
                          .Result
                          .EnsureSuccessful()
                          .Content
                          .ReadAsStringAsync()
                          .Result;
        }

        public Project GetProject()
        {
            return Client.GetJson<Project>(String.Empty);
        }

        public void WriteAllText(string path, string content)
        {
            Client.PutAsync(path, HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("content", content)))
                  .Result
                  .EnsureSuccessful();
        }

        public void Delete(string path)
        {
            Client.DeleteAsync(path)
                  .Result
                  .EnsureSuccessful();
        }
    }
}

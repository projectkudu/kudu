using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.Editor;

namespace Kudu.Client.Editor
{
    public class RemoteProjectSystem : IProjectSystem, IKuduClientCredentials
    {
        private readonly HttpClient _client;
        private ICredentials _credentials;

        public RemoteProjectSystem(string serviceUrl)
        {
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public string ReadAllText(string path)
        {
            // REVIEW: Do we need to url encode?
            // REVIEW: this goes through the same client that set the Accept header to application/json, but we receive text/plain
            return _client.Get("?path=" + path)
                          .EnsureSuccessful()
                          .Content
                          .ReadAsString();
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

        public Project GetProject()
        {
            return _client.GetJson<Project>(String.Empty);
        }

        public void WriteAllText(string path, string content)
        {
            _client.Post("save", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path), new KeyValuePair<string, string>("content", content)))
                   .EnsureSuccessful();
        }

        public void Delete(string path)
        {
            _client.Post("delete", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("path", path)))
                   .EnsureSuccessful();
        }
    }
}

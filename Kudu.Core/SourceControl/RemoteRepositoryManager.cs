using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.SourceControl {
    public class RemoteRepositoryManager : IRepositoryManager {
        private readonly HttpClient _client;

        public RemoteRepositoryManager(string serviceUrl) {            
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public void CreateRepository(RepositoryType type) {
            _client.Post("create", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "type", ((int)type).ToString() }
            })).EnsureSuccessful();
        }

        public RepositoryType GetRepositoryType() {
            return _client.GetJson<RepositoryType>("kind");
        }

        public void Delete() {
            _client.Post("delete", new StringContent(String.Empty))
                   .EnsureSuccessful();
        }

        public IRepository GetRepository() {
            return new RemoteRepository(_client.BaseAddress.OriginalString);
        }
    }
}

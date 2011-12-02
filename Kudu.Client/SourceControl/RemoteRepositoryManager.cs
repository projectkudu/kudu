using System;
using System.Collections.Generic;
using System.Net.Http;
using Kudu.Client.Infrastructure;
using Kudu.Core.SourceControl;

namespace Kudu.Client.SourceControl
{
    public class RemoteRepositoryManager : KuduRemoteClientBase, IRepositoryManager
    {
        public RemoteRepositoryManager(string serviceUrl)
            :base(serviceUrl)
        {
        }

        public void CreateRepository(RepositoryType type)
        {
            _client.PostAsync("create", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("type", type.ToString())))
                   .Result
                   .EnsureSuccessful();
        }

        public RepositoryType GetRepositoryType()
        {
            return _client.GetAsyncJson<RepositoryType>("kind");
        }

        public void Delete()
        {
            _client.PostAsync("delete", new StringContent(String.Empty))
                   .Result
                   .EnsureSuccessful();
        }

        public void CloneRepository(RepositoryType type)
        {
            _client.PostAsync("clone", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("type", type.ToString())))
                   .Result
                   .EnsureSuccessful();
        }

        public IRepository GetRepository()
        {
            return new RemoteRepository(_client.BaseAddress.OriginalString);
        }
    }
}

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
            _client.Post("create", HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("type", type.ToString())))
                   .EnsureSuccessful();
        }

        public RepositoryType GetRepositoryType()
        {
            return _client.GetJson<RepositoryType>("kind");
        }

        public void Delete()
        {
            _client.Post("delete", new StringContent(String.Empty))
                   .EnsureSuccessful();
        }

        public void CloneRepository(string source, RepositoryType type)
        {
            _client.Post("clone",
                         HttpClientHelper.CreateJsonContent(new KeyValuePair<string, string>("source", source),
                                                            new KeyValuePair<string, string>("type", type.ToString()))).EnsureSuccessful();
        }

        public IRepository GetRepository()
        {
            return new RemoteRepository(_client.BaseAddress.OriginalString);
        }
    }
}

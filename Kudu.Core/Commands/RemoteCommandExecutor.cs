using System.Collections.Generic;
using System.Net.Http;
using Kudu.Core.Commands;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment {
    public class RemoteCommandExecutor : ICommandExecutor {
        private readonly HttpClient _client;

        public RemoteCommandExecutor(string serviceUrl) {
            serviceUrl = UrlUtility.EnsureTrailingSlash(serviceUrl);
            _client = HttpClientHelper.Create(serviceUrl);
        }

        public string ExecuteCommand(string command) {
            return _client.Post("run", new FormUrlEncodedContent(new Dictionary<string, string> {
                { "command", command }
            })).EnsureSuccessful()
               .Content
               .ReadAsString();
        }
    }
}
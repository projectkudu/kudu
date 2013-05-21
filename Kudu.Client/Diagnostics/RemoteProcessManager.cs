using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.Diagnostics;

namespace Kudu.Client.Diagnostics
{
    public class RemoteProcessManager : KuduRemoteClientBase
    {
        public RemoteProcessManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public Task<IEnumerable<ProcessInfo>> GetProcessesAsync()
        {
            return Client.GetJsonAsync<IEnumerable<ProcessInfo>>(String.Empty);
        }

        public Task<ProcessInfo> GetCurrentProcessAsync()
        {
            return GetProcessAsync(0);
        }

        public Task<ProcessInfo> GetProcessAsync(int id)
        {
            return Client.GetJsonAsync<ProcessInfo>(id.ToString());
        }

        public async Task KillProcessAsync(int id)
        {
            HttpResponseMessage response = await Client.DeleteAsync(id.ToString());
            response.EnsureSuccessful().Dispose();
        }

        public async Task<Stream> MiniDump(int id = 0, int dumpType = 0)
        {
            string path = id + "/dump";
            if (dumpType != 0)
            {
                path += "?dumpType=" + dumpType;
            }

            HttpResponseMessage response = await Client.GetAsync(path);
            return await response.EnsureSuccessful().Content.ReadAsStreamAsync();
        }
    }
}
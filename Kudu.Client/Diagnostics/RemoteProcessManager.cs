using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Core.Diagnostics;

namespace Kudu.Client.Diagnostics
{
    public class RemoteProcessManager : KuduRemoteClientBase
    {
        // default useCookies to true to make sure client sticky to instances
        public RemoteProcessManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null, bool useCookies = true)
            : base(serviceUrl, credentials, handler, useCookies)
        {
        }

        public Task<IEnumerable<ProcessInfo>> GetProcessesAsync(bool allUsers = false)
        {
            if (!allUsers)
            {
                return Client.GetJsonAsync<IEnumerable<ProcessInfo>>(String.Empty);
            }

            return Client.GetJsonAsync<IEnumerable<ProcessInfo>>("?allUsers=" + allUsers);
        }

        public Task<ProcessInfo> GetCurrentProcessAsync()
        {
            return GetProcessAsync(0);
        }

        public Task<ProcessInfo> GetProcessAsync(int id)
        {
            return Client.GetJsonAsync<ProcessInfo>(id.ToString());
        }

        public async Task KillProcessAsync(int id, bool throwOnError = true)
        {
            try
            {
                HttpResponseMessage response = await Client.DeleteAsync(id.ToString());
                response.EnsureSuccessful().Dispose();
            }
            catch (Exception)
            {
                if (throwOnError)
                {
                    throw;
                }
            }
        }

        public async Task<Stream> MiniDump(int id = 0, int dumpType = 0, string format = null)
        {
            var path = new StringBuilder();
            path.AppendFormat("{0}/dump", id);

            var separator = '?';
            if (dumpType > 0)
            {
                path.AppendFormat("{0}dumpType={1}", separator, dumpType);
                separator = '&';
            }
            if (!String.IsNullOrEmpty(format))
            {
                path.AppendFormat("{0}format={1}", separator, format);
                separator = '&';
            }

            HttpResponseMessage response = await Client.GetAsync(path.ToString());
            return await response.EnsureSuccessful().Content.ReadAsStreamAsync();
        }

        public async Task<Stream> GCDump(int id = 0, int maxDumpCountK = 0, string format = null)
        {
            var path = new StringBuilder();
            path.AppendFormat("{0}/gcdump", id);

            var separator = '?';
            if (maxDumpCountK > 0)
            {
                path.AppendFormat("{0}maxDumpCountK={1}", separator, maxDumpCountK);
                separator = '&';
            }
            if (!String.IsNullOrEmpty(format))
            {
                path.AppendFormat("{0}format={1}", separator, format);
                separator = '&';
            }

            HttpResponseMessage response = await Client.GetAsync(path.ToString());
            return await response.EnsureSuccessful().Content.ReadAsStreamAsync();
        }
    }
}
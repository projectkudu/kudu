using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.SiteExtensions;

namespace Kudu.Client.SiteExtensions
{
    public class RemoteSiteExtensionManager : KuduRemoteClientBase, ISiteExtensionManager
    {
        public RemoteSiteExtensionManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetExtensions(string filter = null)
        {
            if (String.IsNullOrEmpty(filter))
            {
                return await Client.GetJsonAsync<IEnumerable<SiteExtensionInfo>>("?filter=" + filter);
            }
            else
            {
                return await Client.GetJsonAsync<IEnumerable<SiteExtensionInfo>>(String.Empty);
            }
        }

        public async Task<SiteExtensionInfo> GetExtension(string id)
        {
            return await Client.GetJsonAsync<SiteExtensionInfo>(id.ToString(CultureInfo.InvariantCulture));
        }

        public async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info)
        {
            return await Client.PutJsonAsync<SiteExtensionInfo, SiteExtensionInfo>(String.Empty, info);
        }

        public async Task<SiteExtensionInfo> UpdateExtension(SiteExtensionInfo info)
        {
            return await Client.PostJsonAsync<SiteExtensionInfo, SiteExtensionInfo>(String.Empty, info);
        }

        public async Task<bool> UninstallExtension(string id)
        {
            HttpResponseMessage result = await Client.DeleteAsync(id.ToString(CultureInfo.InvariantCulture));
            return await result.EnsureSuccessful().Content.ReadAsAsync<bool>();
        }
    }
}
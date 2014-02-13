using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.SiteExtensions;

namespace Kudu.Client.SiteExtensions
{
    public class RemoteSiteExtensionManager : KuduRemoteClientBase
    {
        public RemoteSiteExtensionManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("remote");

            var separator = '?';
            if (!String.IsNullOrEmpty(filter))
            {
                url.Append(separator);
                url.Append("filter=");
                url.Append(filter);
                separator = '&';
            }

            if (allowPrereleaseVersions)
            {
                url.Append(separator);
                url.Append("allowPrereleaseVersions=");
                url.Append(true);
            }

            return await Client.GetJsonAsync<IEnumerable<SiteExtensionInfo>>(url.ToString());
        }

        public async Task<SiteExtensionInfo> GetRemoteExtension(string id, string version = null)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("remote/");
            url.Append(id);

            if (!String.IsNullOrEmpty(version))
            {
                url.Append("?version=");
                url.Append(version);
            }

            return await Client.GetJsonAsync<SiteExtensionInfo>(url.ToString());
        }

        public async Task<IEnumerable<SiteExtensionInfo>> GetLocalExtensions(string filter = null, bool checkLatest = true)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("local");

            var separator = '?';
            if (!String.IsNullOrEmpty(filter))
            {
                url.Append(separator);
                url.Append("filter=");
                url.Append(filter);
                separator = '&';
            }

            if (checkLatest)
            {
                url.Append(separator);
                url.Append("checkLatest=");
                url.Append(checkLatest);
                separator = '&';
            }

            return await Client.GetJsonAsync<IEnumerable<SiteExtensionInfo>>(url.ToString());
        }

        public async Task<SiteExtensionInfo> GetLocalExtension(string id, bool checkLatest = true)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("local/");
            url.Append(id);

            if (checkLatest)
            {
                url.Append("?checkLatest=");
                url.Append(checkLatest);
            }
            
            return await Client.GetJsonAsync<SiteExtensionInfo>(url.ToString());
        }

        public async Task<SiteExtensionInfo> InstallExtension(SiteExtensionInfo info)
        {
            return await Client.PostJsonAsync<SiteExtensionInfo, SiteExtensionInfo>(String.Empty, info);
        }

        public async Task<bool> UninstallExtension(string id)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("local/");
            url.Append(id);

            HttpResponseMessage result = await Client.DeleteAsync(new Uri(url.ToString()));
            return await result.EnsureSuccessful().Content.ReadAsAsync<bool>();
        }
    }
}
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Kudu.Client.Infrastructure;
using Kudu.Contracts.SiteExtensions;
using Kudu.Services.Arm;
using Newtonsoft.Json.Linq;

namespace Kudu.Client.SiteExtensions
{
    public class RemoteSiteExtensionManager : KuduRemoteClientBase
    {
        public RemoteSiteExtensionManager(string serviceUrl, ICredentials credentials = null, HttpMessageHandler handler = null)
            : base(serviceUrl, credentials, handler)
        {
        }

        public async Task<HttpResponseMessage> GetRemoteExtensions(string filter = null, bool allowPrereleaseVersions = false, string feedUrl = null)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("extensionfeed");

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
                separator = '&';
            }

            if (!string.IsNullOrWhiteSpace(feedUrl))
            {
                url.Append(separator);
                url.Append("feedUrl=");
                url.Append(HttpUtility.UrlEncode(feedUrl));
            }

            return (await Client.GetAsync(url.ToString())).EnsureSuccessful();
        }

        public async Task<HttpResponseMessage> GetRemoteExtension(string id, string version = null, string feedUrl = null)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("extensionfeed/");
            url.Append(id);

            var separator = '?';
            if (!String.IsNullOrEmpty(version))
            {
                url.Append(separator);
                url.Append("version=");
                url.Append(version);
                separator = '&';
            }

            if (!string.IsNullOrWhiteSpace(feedUrl))
            {
                url.Append(separator);
                url.Append("feedUrl=");
                url.Append(HttpUtility.UrlEncode(feedUrl));
            }

            return (await Client.GetAsync(url.ToString())).EnsureSuccessful();
        }

        public async Task<HttpResponseMessage> GetLocalExtensions(string filter = null, bool checkLatest = true)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("siteextensions");

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

            return (await Client.GetAsync(url.ToString())).EnsureSuccessful();
        }

        public async Task<HttpResponseMessage> GetLocalExtension(string id, bool checkLatest = true)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("siteextensions/");
            url.Append(id);

            if (checkLatest)
            {
                url.Append("?checkLatest=");
                url.Append(checkLatest);
            }

            return (await Client.GetAsync(url.ToString())).EnsureSuccessful();
        }

        public async Task<HttpResponseMessage> InstallExtension(string id, string version = null, string feedUrl = null, SiteExtensionInfo.SiteExtensionType? type = null, string installationArgs = null, string packageUri = null)
        {
            var json = new JObject();
            json["version"] = version;
            json["feed_url"] = feedUrl;
            json["installer_command_line_params"] = installationArgs;
            json["packageUri"] = packageUri;
            if (type.HasValue)
            {
                json["type"] = Enum.GetName(typeof(SiteExtensionInfo.SiteExtensionType), type.Value);
            }

            // if it is arm request, payload will be something like below
            /*
                {"properties":{
                    "version": "1.0.0",
                    "feed_url": "https://www.nuget.org/api/v2/"
                }}
             */
            if (Client.DefaultRequestHeaders.Contains(ArmUtils.GeoLocationHeaderKey))
            {
                JObject armProperties = json;
                json = new JObject();
                json["properties"] = armProperties;
            }

            return (await Client.PutAsJsonAsync("siteextensions/" + id, json)).EnsureSuccessful();
        }

        public async Task<HttpResponseMessage> UninstallExtension(string id)
        {
            var url = new StringBuilder(ServiceUrl);
            url.Append("siteextensions/");
            url.Append(id);
            return (await Client.DeleteAsync(url.ToString())).EnsureSuccessful();
        }
    }
}
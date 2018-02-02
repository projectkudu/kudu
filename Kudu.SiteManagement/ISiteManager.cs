using System.Collections.Generic;
using System.Collections.Specialized;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    public interface ISiteManager
    {
        IEnumerable<string> GetSites();
        Site GetSite(string applicationName);
        Task<Site> CreateSiteAsync(string applicationName);

        Task DeleteSiteAsync(string applicationName);
        bool AddSiteBinding(string applicationName, KuduBinding binding);
        bool RemoveSiteBinding(string applicationName, string siteBinding, SiteType siteType);

        NameValueCollection GetAppSettings(string applicationName);
        void RemoveAppSetting(string applicationName, string key);
        void SetAppSetting(string applicationName, string key, string value);
    }
}

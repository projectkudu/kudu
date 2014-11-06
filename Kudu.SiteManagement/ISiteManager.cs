using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kudu.SiteManagement
{
    public interface ISiteManager
    {
        IEnumerable<string> GetSites();
        Site GetSite(string applicationName);
        Task<Site> CreateSiteAsync(string applicationName);

        Task DeleteSiteAsync(string applicationName);
        bool AddSiteBinding(string applicationName, string siteBinding, SiteType siteType);
        bool RemoveSiteBinding(string applicationName, string siteBinding, SiteType siteType);
    }
}

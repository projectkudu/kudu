using System.Collections.Generic;

namespace Kudu.SiteManagement
{
    public interface ISiteManager
    {
        IEnumerable<string> GetSites();
        Site GetSite(string applicationName);
        Site CreateSite(string applicationName);
        void DeleteSite(string applicationName);
        void SetSiteWebRoot(string applicationName, string siteRoot);
        bool AddSiteBinding(string applicationName, string siteBinding, SiteType siteType);
        bool RemoveSiteBinding(string applicationName, string siteBinding, SiteType siteType);
    }
}

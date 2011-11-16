namespace Kudu.SiteManagement
{
    public interface ISiteManager
    {
        Site CreateSite(string applicationName);
        void DeleteSite(string applicationName);
        bool TryCreateDeveloperSite(string applicationName, out string siteUrl);
        void SetDeveloperSiteWebRoot(string applicationName, string siteRoot);
    }
}

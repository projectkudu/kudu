namespace Kudu.SiteManagement
{
    public interface ISiteManager
    {
        Site GetSite(string applicationName);
        Site CreateSite(string applicationName);
        Site CreateSite(string applicationName, string hostname, int port);
        void DeleteSite(string applicationName);
        bool TryCreateDeveloperSite(string applicationName, out string siteUrl);
        void SetDeveloperSiteWebRoot(string applicationName, string siteRoot);
    }
}

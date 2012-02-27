namespace Kudu.SiteManagement
{
    public interface IPathResolver
    {
        string ServiceSitePath { get; }
        string SitesPath { get; }
        string GetApplicationPath(string applicationName);
        string GetLiveSitePath(string applicationName);
        string GetDeveloperApplicationPath(string applicationName);
    }
}

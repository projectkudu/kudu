namespace Kudu.SiteManagement
{
    public interface ISettingsResolver
    {
        string SitesBaseUrl { get; }
        
        string ServiceSitesBaseUrl { get; }

        bool CustomHostNames { get; }
    }
}

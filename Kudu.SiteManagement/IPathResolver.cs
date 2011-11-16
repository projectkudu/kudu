namespace Kudu.SiteManagement
{
    public interface IPathResolver
    {
        string ServiceSitePath { get; }
        string GetApplicationPath(string applicationName);
        string GetDeveloperApplicationPath(string applicationName);
    }
}

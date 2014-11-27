namespace Kudu.SiteManagement
{
    public interface IPathResolver
    {
        string GetApplicationPath(string applicationName);
        string GetLiveSitePath(string applicationName);
    }
}

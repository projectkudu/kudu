namespace Kudu.Web.Models
{
    public interface ISettingsService
    {
        ISettings GetSettings(string siteName);
        void SetConnectionString(string siteName, string name, string connectionString);
        void RemoveConnectionString(string siteName, string name);
        void RemoveAppSetting(string siteName, string key);
        void SetAppSetting(string siteName, string key, string value);
    }
}

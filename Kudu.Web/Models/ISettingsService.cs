namespace Kudu.Web.Models
{
    public interface ISettingsService
    {
        ISettings GetSettings(string userName, string siteName);
        void SetConnectionString(string userName, string siteName, string name, string connectionString);
        void RemoveConnectionString(string userName, string siteName, string name);
        void RemoveAppSetting(string userName, string siteName, string key);
        void SetAppSetting(string userName, string siteName, string key, string value);
    }
}

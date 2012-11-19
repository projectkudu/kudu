using System.Collections.Generic;
using System.Threading.Tasks;

namespace Kudu.Web.Models
{
    public interface ISettingsService
    {
        Task<ISettings> GetSettings(string siteName);
        void SetConnectionString(string siteName, string name, string connectionString);
        void RemoveConnectionString(string siteName, string name);
        void RemoveAppSetting(string siteName, string key);
        void SetAppSetting(string siteName, string key, string value);
        Task SetKuduSetting(string siteName, string key, string value);
        Task SetKuduSettings(string siteName, params KeyValuePair<string, string>[] values);
        Task RemoveKuduSetting(string siteName, string key);
    }
}

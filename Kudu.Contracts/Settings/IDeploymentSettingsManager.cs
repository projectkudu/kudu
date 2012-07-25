using System.Collections.Generic;

namespace Kudu.Contracts.Settings
{
    public interface IDeploymentSettingsManager
    {
        void SetValue(string key, string value);
        IEnumerable<KeyValuePair<string, string>> GetValues();
        string GetValue(string key);
        void DeleteValue(string key);
    }
}

using System.Collections.Generic;

namespace Kudu.Contracts.Settings
{
    public interface IDeploymentSettingsManager
    {
        void SetValue(string key, string value);
        IEnumerable<KeyValuePair<string, string>> GetValues();

        /// <summary>
        /// Gets a value for the key from an unified list of environment, per site settings and defaults.
        /// </summary>
        /// <param name="key">The key to look up</param>
        /// <param name="preventUnification">Do not unify the list, instead using the per site setting value</param>
        /// <returns></returns>
        string GetValue(string key, bool preventUnification);
        void DeleteValue(string key);
    }
}

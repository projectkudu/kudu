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
        /// <param name="onlyPerSite">Only read the per site settings while ignoring the unification list. Default is false</param>
        /// <returns></returns>
        string GetValue(string key, bool onlyPerSite);
        void DeleteValue(string key);

        IEnumerable<ISettingsProvider> SettingsProviders { get; }
    }
}

using System.Collections.Generic;

namespace Kudu.Contracts.Settings
{
    public interface ISettingsProvider
    {
        IEnumerable<KeyValuePair<string, string>> GetValues();
        string GetValue(string key);
        int Priority { get; }
    }
}

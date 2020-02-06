using System;
using System.Collections.Generic;
using System.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Settings
{
    public static class ScmHostingConfigurations
    {
        private readonly static string _configsFile = 
            System.Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\SiteExtensions\kudu\ScmHostingConfigurations.txt");

        private static Dictionary<string, string> _configs;
        private static DateTime _configsTTL;

        public static string GetValue(string key, string defaultValue = null)
        {
            var configs = _configs;
            if (configs == null || DateTime.UtcNow > _configsTTL)
            {
                _configsTTL = DateTime.UtcNow.AddMinutes(10);

                try
                {
                    var settings = FileSystemHelpers.FileExists(_configsFile) 
                        ? FileSystemHelpers.ReadAllText(_configsFile) 
                        : null;

                    KuduEventSource.Log.GenericEvent(
                        ServerConfiguration.GetApplicationName(),
                        $"ScmHostingConfigurations: Update value '{settings}'",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        string.Empty);

                    configs = Parse(settings);
                    _configs = configs;
                }
                catch (Exception ex)
                {
                    KuduEventSource.Log.KuduException(
                        ServerConfiguration.GetApplicationName(),
                        "ScmHostingConfigurations.GetValue",
                        string.Empty,
                        string.Empty,
                        $"ScmHostingConfigurations: Fail to GetValue('{key}')",
                        ex.ToString());
                }
            }

            return (configs == null || !configs.TryGetValue(key, out string value)) ? defaultValue : value;
        }

        public static Dictionary<string, string> Parse(string settings)
        {
            return string.IsNullOrEmpty(settings)
                ? new Dictionary<string, string>()
                : settings
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => s.Split(new[] { '=' }, StringSplitOptions.RemoveEmptyEntries))
                    .Where(a => a.Length == 2)
                    .ToDictionary(a => a[0], a => a[1], StringComparer.OrdinalIgnoreCase);
        }
    }
}

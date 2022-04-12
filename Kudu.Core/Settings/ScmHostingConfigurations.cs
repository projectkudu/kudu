using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using Kudu.Contracts.Settings;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Core.Settings
{
    public static class ScmHostingConfigurations
    {
        public readonly static string ConfigsFile = 
            System.Environment.ExpandEnvironmentVariables(@"%ProgramFiles(x86)%\SiteExtensions\kudu\ScmHostingConfigurations.txt");

        private static Dictionary<string, string> _configs;
        private static DateTime _configsTTL;

        // for mocking purpose
        public static Dictionary<string, string> Config
        {
            get { return _configs; }
            set
            {
                _configs = value;
                _configsTTL = value != null ? DateTime.UtcNow.AddMinutes(10) : DateTime.MinValue;
            }
        }

        public static int ArmRetryAfterSeconds
        {
            get
            {
                const int DefaultArmRetryAfterSeconds = 30;
                if (!int.TryParse(GetValue("ArmRetryAfterSeconds"), out int secs))
                {
                    return DefaultArmRetryAfterSeconds;
                }

                return Math.Max(10, secs);
            }
        }

        public static int StreamCopyBufferSize
        {
            get 
            {
                // We pick a value that is the largest multiple of 4096 that is still smaller than the large object heap threshold (85K).
                // The CopyTo/CopyToAsync buffer is short-lived and is likely to be collected at Gen0, and it offers a significant
                // improvement in Copy performance.
                // see https://referencesource.microsoft.com/#mscorlib/system/io/stream.cs,53
                const int DefaultStreamCopyBufferSize = 81920;

                if (!int.TryParse(GetValue(nameof(StreamCopyBufferSize)), out int value))
                {
                    value = DefaultStreamCopyBufferSize;
                }

                // multiple of 4096 and between range
                return Math.Max(4096, Math.Min(DefaultStreamCopyBufferSize, (value / 4096) * 4096));
            }
        }

        public static bool GetLatestDeploymentOptimized
        {
            get { return GetValue("GetLatestDeploymentOptimized", "1") != "0"; }
        }

        // default = 0, meaning same behavior as today
        // 60, meaning 60s delay + only call after sitepackage update + empty trigger
        public static int FunctionsSyncTriggersDelaySeconds
        {
            get
            {
                if (int.TryParse(GetValue("FunctionsSyncTriggersDelaySeconds"), out int secs)
                    && secs >= 0)
                {
                    return secs;
                }

                return 60;
            }
        }

        // If FunctionsSyncTriggersDelaySeconds = 0, this is a no-op
        // default = 0, blocking for FunctionsSyncTriggersDelaySeconds
        // 1, in the background
        public static bool FunctionsSyncTriggersDelayBackground
        {
            get
            {
                return GetValue("FunctionsSyncTriggersDelayBackground", "1") != "0" && FunctionsSyncTriggersDelaySeconds > 0;
            }
        }

        public static bool DeploymentStatusCompleteFileEnabled
        {
            get { return GetValue("DeploymentStatusCompleteFileEnabled", "1") != "0"; }
        }

        public static int TelemetryIntervalMinutes
        {
            get { return int.TryParse(GetValue("TelemetryIntervalMinutes"), out int value) ? value : 30; }
        }

        public static bool UseMSBuild167ForDotnet31
        {
            // this is disabled by default
            get { return GetValue("UseMSBuild167ForDotnet31", "0") == "1"; }
        }

        public static IPAddress ILBVip
        {
            get { return IPAddress.TryParse(GetValue(SettingsKeys.ILBVip), out var address) ? address : null; }
        }

        public static string GetValue(string key, string defaultValue = null)
        {
            var env = System.Environment.GetEnvironmentVariable($"SCM_{key}");
            if (!string.IsNullOrEmpty(env))
            {
                return env;
            }

            var configs = _configs;
            if (configs == null || DateTime.UtcNow > _configsTTL)
            {
                _configsTTL = DateTime.UtcNow.AddMinutes(10);

                try
                {
                    var settings = FileSystemHelpers.FileExists(ConfigsFile) 
                        ? FileSystemHelpers.ReadAllText(ConfigsFile) 
                        : null;

                    KuduEventSource.Log.GenericEvent(
                        ServerConfiguration.GetRuntimeSiteName(),
                        $"ScmHostingConfigurations: Update value '{settings}'",
                        string.Empty,
                        string.Empty,
                        string.Empty,
                        EnvironmentHelper.KuduVersion.Value,
                        EnvironmentHelper.AppServiceVersion.Value);

                    configs = Parse(settings);
                    _configs = configs;
                }
                catch (Exception ex)
                {
                    KuduEventSource.Log.KuduException(
                        ServerConfiguration.GetRuntimeSiteName(),
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

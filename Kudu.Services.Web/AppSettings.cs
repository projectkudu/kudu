using System;
using System.Configuration;

namespace Kudu.Services.Web
{
    public static class AppSettings
    {
        private const string EnableAuthenticationKey = "enableAuthentication";
        private const string EnableProfilerKey = "enableProfiler";
        private const string EnableSettingsKey = "enableSettings";

        public static bool AuthenticationEnabled
        {
            get
            {
                return GetValue<bool>(EnableAuthenticationKey);
            }
        }

        public static bool ProfilingEnabled
        {
            get
            {
                return GetValue<bool>(EnableProfilerKey);
            }
        }

        public static bool SettingsEnabled
        {
            get
            {
                return GetValue<bool>(EnableSettingsKey);
            }
        }

        public static T GetValue<T>(string key)
        {
            string value = ConfigurationManager.AppSettings[key];
            try
            {
                return (T)Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return default(T);
            }
        }
    }
}
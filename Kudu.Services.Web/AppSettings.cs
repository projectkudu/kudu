using System;
using System.Configuration;

namespace Kudu.Services.Web
{
    public static class AppSettings
    {
        private const string EnableAuthenticationKey = "kudu.enableAuthentication";
        private const string EnableTraceKey = "kudu.enableTrace";
        private const string EnableSettingsKey = "kudu.enableSettings";
        private const string BlockLocalRequests = "kudu.blockLocalRequests";
        private const string NuGetCachePathKey = "nuget.cache";

        public static bool AuthenticationEnabled
        {
            get
            {
                return GetValue<bool>(EnableAuthenticationKey);
            }
        }

        public static bool TraceEnabled
        {
            get
            {
                return GetValue<bool>(EnableTraceKey);
            }
        }

        public static bool SettingsEnabled
        {
            get
            {
                return GetValue<bool>(EnableSettingsKey);
            }
        }

        public static bool BlockLocalhost
        {
            get
            {
                return GetValue<bool>(BlockLocalRequests);
            }
        }

        public static string NuGetCachePath
        {
            get
            {
                return GetValue<string>(NuGetCachePathKey);
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
using System;
using System.Linq;
using System.Configuration;

namespace Kudu.Services.Web
{
    public static class AppSettings
    {
        private const string TraceLevelKey = "kudu.traceLevel";
        private const string BlockLocalRequests = "kudu.blockLocalRequests";
        private const string DisableGitKey = "kudu.disableGit";
        private const string NuGetCachePathKey = "nuget.cache";
        private const string GitUsernameKey = "git.username";
        private const string GitEmailKey = "git.email";

        public static bool TraceEnabled
        {
            get
            {
                return TraceLevel > 0;
            }
        }

        public static int TraceLevel
        {
            get
            {
                return GetValue<int>(TraceLevelKey);
            }
        }

        public static bool BlockLocalhost
        {
            get
            {
                return GetValue<bool>(BlockLocalRequests);
            }
        }

        public static string GitUsername
        {
            get
            {
                return GetValue<string>(GitUsernameKey);
            }
        }

        public static string GitEmail
        {
            get
            {
                return GetValue<string>(GitEmailKey);
            }
        }

        public static string NuGetCachePath
        {
            get
            {
                return GetValue<string>(NuGetCachePathKey);
            }
        }

        public static bool DisableGit
        {
            get
            {
                return GetValue<string>(DisableGitKey) == "1";
            }
        }

        public static T GetValue<T>(string key)
        {
            string value = Environment.GetEnvironmentVariable(key);

            if (String.IsNullOrEmpty(value))
            {
                value = ConfigurationManager.AppSettings[key];
            }

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
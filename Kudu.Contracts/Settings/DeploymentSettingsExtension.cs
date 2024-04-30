using System;
using System.Diagnostics;
using System.IO;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;

namespace Kudu.Contracts.Settings
{
    public static class DeploymentSettingsExtension
    {
        public static readonly TimeSpan DefaultCommandIdleTimeout = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan DefaultLogStreamTimeout = TimeSpan.FromMinutes(120);  // remember to update help message
        public static readonly TimeSpan DefaultHttpClientTimeout = TimeSpan.FromSeconds(100);
        public static readonly TimeSpan MoonCakeDefaultHttpClientTimeout = TimeSpan.FromMinutes(5); // Mooncake default should be 5 min
        public static readonly TimeSpan DefaultWebJobsRestartTime = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan DefaultJobsIdleTimeout = TimeSpan.FromMinutes(2);
        public const TraceLevel DefaultTraceLevel = TraceLevel.Error;

        public const int DefaultMaxJobRunsHistoryCount = 50;

        public static readonly string NuGetSiteExtensionFeedUrl = "https://www.nuget.org/api/v2/";

        // in the future, it should come from HostingConfiguration (@sanmeht)
        public static readonly Lazy<bool> UseSiteExtensionV1 = new Lazy<bool>(() =>
        {
            try
            {
                var path = Path.Combine(System.Environment.GetEnvironmentVariable("SystemRoot"), "temp", SettingsKeys.UseSiteExtensionV1);
                return File.Exists(path);
            }
            catch (Exception)
            {
                // no-op
            }

            return false;
        });

        public static string GetValue(this IDeploymentSettingsManager settings, string key)
        {
            return settings.GetValue(key, onlyPerSite: false);
        }

        public static TraceLevel GetTraceLevel(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.TraceLevel);
            int level;
            if (Int32.TryParse(value, out level))
            {
                if (level <= (int)TraceLevel.Off)
                {
                    return TraceLevel.Off;
                }
                else if (level >= (int)TraceLevel.Verbose)
                {
                    return TraceLevel.Verbose;
                }
                else
                {
                    return (TraceLevel)level;
                }
            }

            return DeploymentSettingsExtension.DefaultTraceLevel;
        }

        public static TimeSpan GetCommandIdleTimeout(this IDeploymentSettingsManager settings)
        {
            return GetTimeSpan(settings, SettingsKeys.CommandIdleTimeout, DefaultCommandIdleTimeout);
        }

        public static TimeSpan GetLogStreamTimeout(this IDeploymentSettingsManager settings)
        {
            return GetTimeSpan(settings, SettingsKeys.LogStreamTimeout, DefaultLogStreamTimeout);
        }

        public static TimeSpan GetHttpClientTimeout(this IDeploymentSettingsManager settings)
        {
            TimeSpan defaultTimeout = DefaultHttpClientTimeout;
            string stampName = GetCurrentStampName();

            if (IsMoonCake(stampName))
            {
                // Update default timeout for SettingsKeys.HttpClientTimeout for Mooncake
                defaultTimeout = MoonCakeDefaultHttpClientTimeout;
            }

            return GetTimeSpan(settings, SettingsKeys.HttpClientTimeout, defaultTimeout);
        }

        public static bool IsMoonCake(string stampName)
        {
            if(!string.IsNullOrEmpty(stampName) && stampName.StartsWith("cnws", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        public static string GetCurrentStampName()
        {
            return System.Environment.GetEnvironmentVariable("WEBSITE_CURRENT_STAMPNAME");
        }

        public static string GetPostDeploymentActionsDir(this IDeploymentSettingsManager settings, string defaultPath)
        {
            string value = settings.GetValue(SettingsKeys.PostDeploymentActionsDirectory);
            return !String.IsNullOrEmpty(value) ? value : defaultPath;
        }

        public static string GetGitUsername(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.GitUsername);
            return !String.IsNullOrEmpty(value) ? value : "unknown";
        }

        public static string GetGitEmail(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.GitEmail);
            return !String.IsNullOrEmpty(value) ? value : "unknown";
        }

        public static TimeSpan GetWebJobsRestartTime(this IDeploymentSettingsManager settings)
        {
            return GetTimeSpan(settings, SettingsKeys.WebJobsRestartTime, DefaultWebJobsRestartTime);
        }

        public static TimeSpan GetWebJobsIdleTimeout(this IDeploymentSettingsManager settings)
        {
            return GetTimeSpan(settings, SettingsKeys.WebJobsIdleTimeoutInSeconds, DefaultJobsIdleTimeout);
        }

        public static int GetWebJobsHistorySize(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.WebJobsHistorySize);
            int maxJobRunsHistoryCount;
            if (Int32.TryParse(value, out maxJobRunsHistoryCount) && maxJobRunsHistoryCount > 0)
            {
                return maxJobRunsHistoryCount;
            }

            return DefaultMaxJobRunsHistoryCount;
        }

        public static bool IsWebJobsStopped(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.WebJobsStopped);

            return StringUtils.IsTrueLike(value);
        }

        public static bool IsWebJobsScheduleDisabled(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.WebJobsDisableSchedule);

            return StringUtils.IsTrueLike(value);
        }

        public static bool LogTriggeredJobsToAppLogs(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.WebJobsLogTriggeredJobsToAppLogs);

            return StringUtils.IsTrueLike(value);
        }

        public static string GetBranch(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.Branch, onlyPerSite: true);
            if (!String.IsNullOrEmpty(value))
            {
                return value;
            }

            return settings.GetValue(SettingsKeys.DeploymentBranch);
        }

        public static void SetBranch(this IDeploymentSettingsManager settings, string branchName)
        {
            // If we're updating branch, clear out the legacy value first
            settings.DeleteValue(SettingsKeys.Branch);
            settings.SetValue(SettingsKeys.DeploymentBranch, branchName);
        }

        /// <summary>
        /// Determines if Kudu should perform shallow clones (--depth 1) when attempting to perform the first fetch from a remote Git repository.
        /// </summary>
        public static bool AllowShallowClones(this IDeploymentSettingsManager settings)
        {
            return StringUtils.IsTrueLike(settings.GetValue(SettingsKeys.UseShallowClone));
        }

        // allow /deploy endpoint
        public static bool IsScmEnabled(this IDeploymentSettingsManager settings)
        {
            string scmType = settings.GetValue(SettingsKeys.ScmType);
            return scmType != ScmType.None && scmType != ScmType.Tfs && scmType != ScmType.TfsGit;
        }

        public static string GetRepositoryPath(this IDeploymentSettingsManager settings)
        {
            string repositoryPath = settings.GetValue(SettingsKeys.RepositoryPath);
            if (!String.IsNullOrEmpty(repositoryPath))
            {
                return System.Environment.ExpandEnvironmentVariables(repositoryPath);
            }

            // in case of no repository, we will default to webroot (preferring inplace).
            if (settings.NoRepository())
            {
                return Constants.WebRoot;
            }

            return Constants.RepositoryPath;
        }

        public static string GetTargetPath(this IDeploymentSettingsManager settings)
        {
            string targetPath = settings.GetValue(SettingsKeys.TargetPath);
            if (!String.IsNullOrEmpty(targetPath))
            {
                return System.Environment.ExpandEnvironmentVariables(targetPath);
            }

            return null;
        }

        public static bool NoRepository(this IDeploymentSettingsManager settings)
        {
            return settings.GetValue(SettingsKeys.NoRepository) == "1";
        }

        public static bool ShouldUpdateSubmodules(this IDeploymentSettingsManager settings)
        {
            return settings.GetValue(SettingsKeys.DisableSubmodules) != "1";
        }

        public static string GetWebSiteSku(this IDeploymentSettingsManager settings)
        {
            string siteSku = Environment.GetEnvironmentVariable(SettingsKeys.WebSiteSku);
            if (String.IsNullOrEmpty(siteSku))
            {
                siteSku = settings.GetValue(SettingsKeys.WebSiteSku);
            }

            return siteSku;
        }

        private static TimeSpan GetTimeSpan(IDeploymentSettingsManager settings, string settingsKey, TimeSpan defaultValue)
        {
            string value = settings.GetValue(settingsKey);
            int seconds;
            if (Int32.TryParse(value, out seconds))
            {
                return TimeSpan.FromSeconds(seconds >= 0 ? seconds : 0);
            }

            return defaultValue;
        }

        public static string GetSiteExtensionRemoteUrl(this IDeploymentSettingsManager settings, out bool isDefault)
        {
            string value = settings.GetValue(SettingsKeys.SiteExtensionsFeedUrl);
            isDefault = String.IsNullOrEmpty(value);
            return !String.IsNullOrEmpty(value) ? value : NuGetSiteExtensionFeedUrl;
        }

        public static bool UseLibGit2SharpRepository(this IDeploymentSettingsManager settings)
        {
            return settings.GetValue(SettingsKeys.UseLibGit2SharpRepository) != "0";
        }

        public static bool TouchWatchedFileAfterDeployment(this IDeploymentSettingsManager settings)
        {
            return settings.GetValue(SettingsKeys.TouchWebConfigAfterDeployment) != "0";
        }

        public static bool IsDockerCiEnabled(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.DockerCiEnabled);
            return StringUtils.IsTrueLike(value);
        }

        public static bool RestartAppOnGitDeploy(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.RestartAppAfterDeployment);

            // Default is true
            return value == null || StringUtils.IsTrueLike(value);
        }

        public static bool RecylePreviewEnabled(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.RecyclePreviewEnabled);

            if (!string.IsNullOrEmpty(value))
            {
                return StringUtils.IsTrueLike(value);
            }

            return "1" == settings.GetHostingConfiguration(SettingsKeys.RecyclePreviewEnabled, defaultValue: null);
        }

        public static bool DoBuildDuringDeployment(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.DoBuildDuringDeployment);

            // A default value should be set on a per-deployment basis depending on the context, but
            // returning true by default here as an indicator of generally expected behavior
            return value == null || StringUtils.IsTrueLike(value);
        }

        public static string GetDoBuildDuringDeploymentAppSettingValue(this IDeploymentSettingsManager settings)
        {
            return settings.GetValue(SettingsKeys.DoBuildDuringDeployment);
        }

        public static bool GetUseSiteExtensionV2(this IDeploymentSettingsManager settings)
        {
            var value = settings.GetValue(SettingsKeys.UseSiteExtensionV1);
            if (!string.IsNullOrEmpty(value))
            {
                return !StringUtils.IsTrueLike(value);
            }

            return !UseSiteExtensionV1.Value;
        }

        public static bool RunFromLocalZip(this IDeploymentSettingsManager settings)
        {
            return settings.GetFromFromZipAppSettingValue() == "1";
        }

        public static bool RunFromRemoteZip(this IDeploymentSettingsManager settings)
        {
            var value = settings.GetFromFromZipAppSettingValue();

            return value != null && value.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetFromFromZipAppSettingValue(this IDeploymentSettingsManager settings)
        {
            // Try both the old and new app setting names
            string runFromZip = settings.GetValue(SettingsKeys.RunFromZip);
            if (String.IsNullOrEmpty(runFromZip))
            {
                runFromZip = settings.GetValue(SettingsKeys.RunFromZipOld);
            }

            return runFromZip;
        }

        public static bool RunFromZip(this IDeploymentSettingsManager settings)
            => settings.RunFromLocalZip() || settings.RunFromRemoteZip();

        public static int GetMaxZipPackageCount(this IDeploymentSettingsManager settings)
        {
            int DEFAULT_ALLOWED_ZIPS = 5;
            int MIN_ALLOWED_ZIPS = 1;

            string maxZipPackageCount = settings.GetValue(SettingsKeys.MaxZipPackageCount);
            if(Int32.TryParse(maxZipPackageCount, out int totalAllowedZips))
            {
                return totalAllowedZips < MIN_ALLOWED_ZIPS ? MIN_ALLOWED_ZIPS : totalAllowedZips;
            }

            return DEFAULT_ALLOWED_ZIPS;
        }

        public static bool GetZipDeployDoNotPreserveFileTime(this IDeploymentSettingsManager settings)
        {
            return "1" == settings.GetValue(SettingsKeys.ZipDeployDoNotPreserveFileTime);
        }

        public static bool GetUseOriginalHostForReference(this IDeploymentSettingsManager settings)
        {
            return "1" == settings.GetValue(SettingsKeys.UseOriginalHostForReference);
        }
    }
}
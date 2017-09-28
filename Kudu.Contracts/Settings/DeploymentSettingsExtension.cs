using System;
using System.Diagnostics;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.SourceControl;

namespace Kudu.Contracts.Settings
{
    public static class DeploymentSettingsExtension
    {
        public static readonly TimeSpan DefaultCommandIdleTimeout = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan DefaultLogStreamTimeout = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan DefaultWebJobsRestartTime = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan DefaultJobsIdleTimeout = TimeSpan.FromMinutes(2);
        public const TraceLevel DefaultTraceLevel = TraceLevel.Error;

        public const int DefaultMaxJobRunsHistoryCount = 50;

        public static readonly string DefaultSiteExtensionFeedUrl = "https://www.siteextensions.net/api/v2/";
        public static readonly string NuGetSiteExtensionFeedUrl = "https://www.nuget.org/api/v2/";

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

        public static string GetSiteExtensionRemoteUrl(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.SiteExtensionsFeedUrl);
            return !String.IsNullOrEmpty(value) ? value : DefaultSiteExtensionFeedUrl;
        }

        public static bool UseLibGit2SharpRepository(this IDeploymentSettingsManager settings)
        {
            return settings.GetValue(SettingsKeys.UseLibGit2SharpRepository) != "0";
        }

        public static bool TouchWebConfigAfterDeployment(this IDeploymentSettingsManager settings)
        {
            return settings.GetValue(SettingsKeys.TouchWebConfigAfterDeployment) != "0";
        }

        public static bool IsDockerCiEnabled(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.DockerCiEnabled);
            return StringUtils.IsTrueLike(value);
        }

        public static bool RestartAppContainerOnGitDeploy(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.LinuxRestartAppContainerAfterDeployment);

            // Default is true
            return value == null || StringUtils.IsTrueLike(value);
        }

        public static bool DoBuildDuringDeployment(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.DoBuildDuringDeployment);

            // A default value should be set on a per-deployment basis depending on the context, but
            // returning true by default here as an indicator of generally expected behavior
            return value == null || StringUtils.IsTrueLike(value);
        }
    }
}
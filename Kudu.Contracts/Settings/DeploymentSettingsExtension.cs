using System;
using System.Diagnostics;
using Kudu.Contracts.SourceControl;

namespace Kudu.Contracts.Settings
{
    public static class DeploymentSettingsExtension
    {
        public static TimeSpan DefaultCommandIdleTimeout = TimeSpan.FromSeconds(180);
        public const TraceLevel DefaultTraceLevel = TraceLevel.Error;
        public const string DefaultGitUsername = "kudu";
        public const string DefaultGitEmail = "kudu";

        public static TraceLevel GetTraceLevel(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.TraceLevel);
            int level;
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out level))
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
            string value = settings.GetValue(SettingsKeys.CommandIdleTimeout);
            int seconds;
            if (!String.IsNullOrEmpty(value) && Int32.TryParse(value, out seconds))
            {
                return TimeSpan.FromSeconds(seconds);
            }

            return DeploymentSettingsExtension.DefaultCommandIdleTimeout;
        }

        public static string GetGitUsername(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.GitUsername);
            return !String.IsNullOrEmpty(value) ? value : DefaultGitUsername;
        }

        public static string GetGitEmail(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.GitEmail);
            return !String.IsNullOrEmpty(value) ? value : DefaultGitEmail;
        }

        public static ScmType GetScmType(this IDeploymentSettingsManager settings)
        {
            string value = settings.GetValue(SettingsKeys.ScmType);
            if (String.IsNullOrEmpty(value))
            {
                return ScmType.Null;
            }

            return (ScmType)Enum.Parse(typeof(ScmType), value);
        }

        // allow git push, clone, /deploy endpoints
        public static bool IsGitEnabled(this IDeploymentSettingsManager settings)
        {
            ScmType scmType = settings.GetScmType();
            return scmType != ScmType.None && scmType != ScmType.Tfs;
        }
    }
}
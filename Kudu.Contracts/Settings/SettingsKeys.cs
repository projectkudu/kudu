﻿namespace Kudu.Contracts.Settings
{
    public static class SettingsKeys
    {
        /// <remarks>
        /// Legacy value that is superseded by DeploymentBranch
        /// </remarks>
        internal const string Branch = "branch";

        public const string DeploymentBranch = "deployment_branch";
        public const string BuildArgs = "SCM_BUILD_ARGS";
        public const string ScriptGeneratorArgs = "SCM_SCRIPT_GENERATOR_ARGS";
        public const string TraceLevel = "SCM_TRACE_LEVEL";
        public const string CommandIdleTimeout = "SCM_COMMAND_IDLE_TIMEOUT";
        public const string LogStreamTimeout = "SCM_LOGSTREAM_TIMEOUT";
        public const string HttpClientTimeout = "SCM_HTTPCLIENT_TIMEOUT";
        public const string GitUsername = "SCM_GIT_USERNAME";
        public const string GitEmail = "SCM_GIT_EMAIL";
        public const string ScmType = "ScmType";
        public const string UseShallowClone = "SCM_USE_SHALLOW_CLONE";
        public const string UseSiteExtensionV1 = "SCM_USE_SITEEXTENSION_V1";
        public const string Command = "COMMAND";
        public const string Project = "PROJECT";
        public const string WorkerCommand = "WORKER_COMMAND";
        public const string TargetPath = "SCM_TARGET_PATH";
        public const string WebJobsRootPath = "WEBJOBS_ROOT_PATH";
        public const string RepositoryPath = "SCM_REPOSITORY_PATH";
        public const string NoRepository = "SCM_NO_REPOSITORY";
        public const string UseLibGit2SharpRepository = "SCM_USE_LIBGIT2SHARP_REPOSITORY";
        public const string SkipSslValidation = "SCM_SKIP_SSL_VALIDATION";
        public const string SkipAseSslValidation = "SCM_SKIP_ASE_SSL_VALIDATION";
        // Free, Shared, Basic, Standard, Premium
        public const string WebSiteSku = "WEBSITE_SKU";
        public const string WebSiteName = "WEBSITE_SITE_NAME";
        public const string WebSiteHostName = "WEBSITE_HOSTNAME";
        public const string WebSiteOwnerName = "WEBSITE_OWNER_NAME";
        public const string WebJobsRestartTime = "WEBJOBS_RESTART_TIME";
        public const string WebJobsIdleTimeoutInSeconds = "WEBJOBS_IDLE_TIMEOUT";
        public const string WebJobsHistorySize = "WEBJOBS_HISTORY_SIZE";
        public const string WebJobsStopped = "WEBJOBS_STOPPED";
        public const string WebJobsDisableSchedule = "WEBJOBS_DISABLE_SCHEDULE";
        public const string WebJobsLogTriggeredJobsToAppLogs = "WEBJOBS_LOG_TRIGGERED_JOBS_TO_APP_LOGS";
        public const string PostDeploymentActionsDirectory = "SCM_POST_DEPLOYMENT_ACTIONS_PATH";
        public const string DisableSubmodules = "SCM_DISABLE_SUBMODULES";
        public const string SiteExtensionsFeedUrl = "SCM_SITEEXTENSIONS_FEED_URL";
        public const string DisableDeploymentOnPush = "SCM_DISABLE_DEPLOY_ON_PUSH";
        public const string TouchWebConfigAfterDeployment = "SCM_TOUCH_WEBCONFIG_AFTER_DEPLOYMENT";
        public const string MaxRandomDelayInSec = "SCM_MAX_RANDOM_START_DELAY";
        public const string DockerCiEnabled = "DOCKER_ENABLE_CI";

        // This app-setting works for all kinds of apps, including classic Windows apps (not just container-based apps).
        // To disable its effect on classic Windows apps, set WEBSITE_RECYCLE_PREVIEW_ENABLED=0.
        public const string RestartAppAfterDeployment = "SCM_RESTART_APP_CONTAINER_AFTER_DEPLOYMENT"; 

        public const string DoBuildDuringDeployment = "SCM_DO_BUILD_DURING_DEPLOYMENT";
        public const string RunFromZipOld = "WEBSITE_RUN_FROM_ZIP";  // Old name, will eventually go away
        public const string RunFromZip = "WEBSITE_RUN_FROM_PACKAGE";

        // Temporary flag intended only for testing purposes. Will not be supported for too long.
        public const string RecyclePreviewEnabled = "WEBSITE_RECYCLE_PREVIEW_ENABLED";

        public const string MaxZipPackageCount = "SCM_MAX_ZIP_PACKAGE_COUNT";
        public const string ZipDeployDoNotPreserveFileTime = "SCM_ZIPDEPLOY_DONOT_PRESERVE_FILETIME";

        public const string UseMSBuild16 = "SCM_USE_MSBUILD_16";
        public const string UseMSBuild1607 = "SCM_USE_MSBUILD_1607";
        
        public const string MSBuildVersion = "SCM_MSBUILD_VERSION";

        public const string UseOriginalHostForReference = "SCM_USE_ORIGINALHOST_FOR_REFERENCE";

        public const string ILBVip = "ILB_VIP";
    }
}
